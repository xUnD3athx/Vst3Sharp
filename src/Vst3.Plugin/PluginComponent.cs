using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vst3.Interop;

namespace Vst3.Plugin;

/// <summary>
/// Combined component / audio processor / edit controller (VST3 single component effect).
/// Three vtable views share one managed instance and one reference count.
/// </summary>
internal unsafe sealed class PluginComponent : ComObject
{
    private const double MaxGain = 2.0;

    private static nint* s_componentVTable;
    private static nint* s_processorVTable;
    private static nint* s_controllerVTable;

    private double _gainNormalized = 0.5;
    private int _maxBlockSize = 128;
    private double _sampleRate = 48000.0;
    private byte _active;
    private byte _processing;
    private void* _componentHandler;
    private double _midiGate = 1.0;
    private byte _sidechainActive;
    private double _sidechainAmount;

    private const uint GainParameterId = 0;
    private const uint SidechainAmountParameterId = 1;

    static PluginComponent()
    {
        s_componentVTable = BuildVtable(new nint[]
        {
            (nint)(delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)&QueryInterface,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&AddRef,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&Release,
            (nint)(delegate* unmanaged[Stdcall]<void*, void*, int>)&Initialize,
            (nint)(delegate* unmanaged[Stdcall]<void*, int>)&Terminate,
            (nint)(delegate* unmanaged[Stdcall]<void*, Guid*, int>)&GetControllerClassId,
            (nint)(delegate* unmanaged[Stdcall]<void*, int, int>)&SetIoMode,
            (nint)(delegate* unmanaged[Stdcall]<void*, int, int, int>)&GetBusCount,
            (nint)(delegate* unmanaged[Stdcall]<void*, int, int, int, BusInfo*, int>)&GetBusInfo,
            (nint)(delegate* unmanaged[Stdcall]<void*, void*, void*, int>)&GetRoutingInfo,
            (nint)(delegate* unmanaged[Stdcall]<void*, int, int, int, byte, int>)&ActivateBus,
            (nint)(delegate* unmanaged[Stdcall]<void*, byte, int>)&SetActive,
            (nint)(delegate* unmanaged[Stdcall]<void*, void*, int>)&SetState,
            (nint)(delegate* unmanaged[Stdcall]<void*, void*, int>)&GetState,
        });

        s_processorVTable = BuildVtable(new nint[]
        {
            (nint)(delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)&QueryInterface,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&AddRef,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&Release,
            (nint)(delegate* unmanaged[Stdcall]<void*, ulong*, int, ulong*, int, int>)&SetBusArrangements,
            (nint)(delegate* unmanaged[Stdcall]<void*, int, int, ulong*, int>)&GetBusArrangement,
            (nint)(delegate* unmanaged[Stdcall]<void*, int, int>)&CanProcessSampleSize,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&GetLatencySamples,
            (nint)(delegate* unmanaged[Stdcall]<void*, ProcessSetup*, int>)&SetupProcessing,
            (nint)(delegate* unmanaged[Stdcall]<void*, byte, int>)&SetProcessing,
            (nint)(delegate* unmanaged[Stdcall]<void*, ProcessData*, int>)&Process,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&GetTailSamples,
        });

        s_controllerVTable = BuildVtable(new nint[]
        {
            (nint)(delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)&QueryInterface,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&AddRef,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint>)&Release,
            (nint)(delegate* unmanaged[Stdcall]<void*, void*, int>)&Initialize,
            (nint)(delegate* unmanaged[Stdcall]<void*, int>)&Terminate,
            (nint)(delegate* unmanaged[Stdcall]<void*, void*, int>)&SetComponentState,
            (nint)(delegate* unmanaged[Stdcall]<void*, void*, int>)&SetState,
            (nint)(delegate* unmanaged[Stdcall]<void*, void*, int>)&GetState,
            (nint)(delegate* unmanaged[Stdcall]<void*, int>)&GetParameterCount,
            (nint)(delegate* unmanaged[Stdcall]<void*, int, ParameterInfo*, int>)&GetParameterInfo,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint, double, char*, int>)&GetParamStringByValue,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint, char*, double*, int>)&GetParamValueByString,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint, double, double>)&NormalizedParamToPlain,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint, double, double>)&PlainParamToNormalized,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint, double>)&GetParamNormalized,
            (nint)(delegate* unmanaged[Stdcall]<void*, uint, double, int>)&SetParamNormalized,
            (nint)(delegate* unmanaged[Stdcall]<void*, void*, int>)&SetComponentHandler,
            (nint)(delegate* unmanaged[Stdcall]<void*, byte*, void*>)&CreateView,
        });
    }

    public PluginComponent()
    {
        Initialize(new nint[] { (nint)s_componentVTable, (nint)s_processorVTable, (nint)s_controllerVTable });
    }

    private static nint* BuildVtable(nint[] entries)
    {
        nint* vtable = (nint*)NativeMemory.Alloc((nuint)entries.Length, (nuint)sizeof(nint));
        for (int i = 0; i < entries.Length; i++)
            vtable[i] = entries[i];

        return vtable;
    }

    public override bool TryGetView(Guid iid, out void* view)
    {
        if (iid == IIds.FUnknown || iid == IIds.IPluginBase || iid == IIds.IComponent)
        {
            view = View(0);
            return true;
        }

        if (iid == IIds.IAudioProcessor)
        {
            view = View(1);
            return true;
        }

        if (iid == IIds.IEditController)
        {
            view = View(2);
            return true;
        }

        view = null;
        return false;
    }

    // ----- FUnknown -----

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int QueryInterface(void* self, Guid* iid, void** obj)
    {
        if (iid == null || obj == null)
            return Result.InvalidArgument;

        *obj = null;
        var instance = Self<PluginComponent>(self);
        if (!instance.TryGetView(*iid, out void* view))
            return Result.NoInterface;

        instance.AddRefCore();
        *obj = view;
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static uint AddRef(void* self) => Self<PluginComponent>(self).AddRefCore();

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static uint Release(void* self) => Self<PluginComponent>(self).ReleaseCore();

    // ----- IPluginBase -----

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int Initialize(void* self, void* context) => Result.Ok;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int Terminate(void* self) => Result.Ok;

    // ----- IComponent -----

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetControllerClassId(void* self, Guid* classId) => Result.NotImplemented;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetIoMode(void* self, int mode) => Result.NotImplemented;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetBusCount(void* self, int mediaType, int direction)
    {
        if (mediaType == MediaTypes.Audio)
        {
            if (direction == BusDirections.Input)
                return 2;
            if (direction == BusDirections.Output)
                return 1;
            return 0;
        }

        if (mediaType == MediaTypes.Event)
            return direction == BusDirections.Input ? 1 : 0;

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetBusInfo(void* self, int mediaType, int direction, int index, BusInfo* info)
    {
        if (info == null || index < 0)
            return Result.InvalidArgument;

        info->MediaType = mediaType;
        info->Direction = direction;
        info->BusType = BusTypes.Main;
        info->Flags = BusFlags.DefaultActive;

        if (mediaType == MediaTypes.Audio)
        {
            if (direction == BusDirections.Input)
            {
                if (index == 0)
                {
                    info->ChannelCount = 2;
                    info->SetName("Input");
                    return Result.Ok;
                }

                if (index == 1)
                {
                    info->ChannelCount = 2;
                    info->BusType = BusTypes.Aux;
                    info->Flags = 0;
                    info->SetName("Sidechain");
                    return Result.Ok;
                }

                return Result.InvalidArgument;
            }

            if (direction == BusDirections.Output && index == 0)
            {
                info->ChannelCount = 2;
                info->SetName("Output");
                return Result.Ok;
            }

            return Result.InvalidArgument;
        }

        if (mediaType == MediaTypes.Event && direction == BusDirections.Input && index == 0)
        {
            info->ChannelCount = 16;
            info->SetName("MIDI In");
            return Result.Ok;
        }

        return Result.InvalidArgument;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetRoutingInfo(void* self, void* inInfo, void* outInfo) => Result.NotImplemented;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int ActivateBus(void* self, int mediaType, int direction, int index, byte state)
    {
        if (mediaType == MediaTypes.Audio && direction == BusDirections.Input && index == 1)
            Self<PluginComponent>(self)._sidechainActive = state;

        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetActive(void* self, byte state)
    {
        Self<PluginComponent>(self)._active = state;
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetState(void* self, void* stream) => Result.NotImplemented;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetState(void* self, void* stream) => Result.NotImplemented;

    // ----- IAudioProcessor -----

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetBusArrangements(void* self, ulong* inputs, int numIns, ulong* outputs, int numOuts)
    {
        if (numOuts != 1 || outputs == null || (outputs[0] & 3UL) != 3UL)
            return Result.False;

        if (numIns < 1 || numIns > 2 || inputs == null || (inputs[0] & 3UL) != 3UL)
            return Result.False;

        if (numIns == 2 && inputs[1] != 0 && (inputs[1] & 3UL) != 3UL)
            return Result.False;

        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetBusArrangement(void* self, int direction, int index, ulong* arrangement)
    {
        if (arrangement == null)
            return Result.InvalidArgument;

        if (direction == BusDirections.Input && (index == 0 || index == 1))
        {
            *arrangement = 3UL;
            return Result.Ok;
        }

        if (direction == BusDirections.Output && index == 0)
        {
            *arrangement = 3UL;
            return Result.Ok;
        }

        return Result.InvalidArgument;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int CanProcessSampleSize(void* self, int symbolicSampleSize) =>
        symbolicSampleSize == SampleSizes.Sample32 ? Result.Ok : Result.False;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static uint GetLatencySamples(void* self) => 0;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetupProcessing(void* self, ProcessSetup* setup)
    {
        if (setup == null)
            return Result.InvalidArgument;

        if (setup->SymbolicSampleSize != SampleSizes.Sample32)
            return Result.False;

        var instance = Self<PluginComponent>(self);
        instance._maxBlockSize = setup->MaxSamplesPerBlock;
        instance._sampleRate = setup->SampleRate;
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetProcessing(void* self, byte state)
    {
        Self<PluginComponent>(self)._processing = state;
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int Process(void* self, ProcessData* data)
    {
        if (data == null)
            return Result.InvalidArgument;

        int numSamples = data->NumSamples;
        if (numSamples <= 0)
            return Result.Ok;

        AudioBusBuffers* outputs = data->Outputs;
        if (outputs == null || data->NumOutputs == 0)
            return Result.Ok;

        var instance = Self<PluginComponent>(self);
        ApplyEvents(data, instance);

        float gain = (float)(instance._gainNormalized * MaxGain) * (float)instance._midiGate;
        gain *= ComputeSidechainDuck(data, instance);

        int outChannels = outputs[0].NumChannels;
        float** outPointers = outputs[0].ChannelBuffers32;

        AudioBusBuffers* inputs = data->Inputs;
        if (inputs == null || data->NumInputs == 0)
        {
            for (int channel = 0; channel < outChannels; channel++)
            {
                float* destination = outPointers[channel];
                for (int i = 0; i < numSamples; i++)
                    destination[i] = 0.0f;
            }

            return Result.Ok;
        }

        int channels = Math.Min(inputs[0].NumChannels, outChannels);
        float** inPointers = inputs[0].ChannelBuffers32;

        for (int channel = 0; channel < channels; channel++)
        {
            float* source = inPointers[channel];
            float* destination = outPointers[channel];
            for (int i = 0; i < numSamples; i++)
                destination[i] = source[i] * gain;
        }

        for (int channel = channels; channel < outChannels; channel++)
        {
            float* destination = outPointers[channel];
            for (int i = 0; i < numSamples; i++)
                destination[i] = 0.0f;
        }

        return Result.Ok;
    }

    private static void ApplyEvents(ProcessData* data, PluginComponent instance)
    {
        void* list = data->InputEvents;
        if (list == null)
            return;

        nint* vtable = *(nint**)list;
        var getEventCount = (delegate* unmanaged[Stdcall]<void*, int>)vtable[3];
        var getEvent = (delegate* unmanaged[Stdcall]<void*, int, Event*, int>)vtable[4];

        int count = getEventCount(list);
        for (int i = 0; i < count; i++)
        {
            Event e = default;
            if (getEvent(list, i, &e) != Result.Ok)
                continue;

            if (e.Type == EventTypes.NoteOn)
                instance._midiGate = Math.Clamp(e.NoteOn.Velocity, 0.0f, 1.0f);
            else if (e.Type == EventTypes.NoteOff)
                instance._midiGate = 1.0;
        }
    }

    private static float ComputeSidechainDuck(ProcessData* data, PluginComponent instance)
    {
        double amount = instance._sidechainAmount;
        if (amount <= 0.0)
            return 1.0f;

        if (data->NumInputs < 2)
            return 1.0f;

        AudioBusBuffers* inputs = data->Inputs;
        if (inputs == null || inputs[1].NumChannels <= 0)
            return 1.0f;

        float** channels = inputs[1].ChannelBuffers32;
        if (channels == null)
            return 1.0f;

        float peak = 0.0f;
        for (int channel = 0; channel < inputs[1].NumChannels; channel++)
        {
            float* samples = channels[channel];
            for (int i = 0; i < data->NumSamples; i++)
            {
                float value = samples[i];
                if (value < 0.0f)
                    value = -value;
                if (value > peak)
                    peak = value;
            }
        }

        if (peak <= 0.0001f)
            return 1.0f;

        if (peak > 1.0f)
            peak = 1.0f;

        return (float)(1.0 - amount * peak);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static uint GetTailSamples(void* self) => 0;

    // ----- IEditController -----

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetComponentState(void* self, void* stream) => Result.NotImplemented;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetParameterCount(void* self) => 2;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetParameterInfo(void* self, int index, ParameterInfo* info)
    {
        if (info == null)
            return Result.InvalidArgument;

        info->Id = (uint)index;
        info->StepCount = 0;
        info->UnitId = 0;
        info->Flags = ParameterFlags.CanAutomate;

        if (index == 0)
        {
            info->SetTitle("Gain");
            info->SetShortTitle("Gain");
            info->SetUnits("x");
            info->DefaultNormalizedValue = 0.5;
            return Result.Ok;
        }

        if (index == 1)
        {
            info->SetTitle("SC Amount");
            info->SetShortTitle("SC");
            info->SetUnits("%");
            info->DefaultNormalizedValue = 0.0;
            return Result.Ok;
        }

        return Result.InvalidArgument;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetParamStringByValue(void* self, uint id, double value, char* text)
    {
        if (text == null)
            return Result.InvalidArgument;

        Span<char> buffer = stackalloc char[64];
        int written;

        if (id == GainParameterId)
        {
            double plain = value * MaxGain;
            if (!plain.TryFormat(buffer, out written, "0.00", CultureInfo.InvariantCulture))
                return Result.InternalError;

            buffer[written++] = ' ';
            buffer[written++] = 'x';
        }
        else if (id == SidechainAmountParameterId)
        {
            double percent = value * 100.0;
            if (!percent.TryFormat(buffer, out written, "0", CultureInfo.InvariantCulture))
                return Result.InternalError;

            buffer[written++] = '%';
        }
        else
        {
            return Result.InvalidArgument;
        }

        if (written >= String128.Capacity)
            written = String128.Capacity - 1;

        for (int i = 0; i < written; i++)
            text[i] = buffer[i];

        text[written] = '\0';
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetParamValueByString(void* self, uint id, char* text, double* value)
    {
        if (text == null || value == null)
            return Result.InvalidArgument;

        string source = String128.Read(text).TrimEnd('x', 'X', '%', ' ');
        if (!double.TryParse(source, NumberStyles.Float, CultureInfo.InvariantCulture, out double plain))
            return Result.False;

        if (id == GainParameterId)
        {
            *value = Math.Clamp(plain / MaxGain, 0.0, 1.0);
            return Result.Ok;
        }

        if (id == SidechainAmountParameterId)
        {
            *value = Math.Clamp(plain / 100.0, 0.0, 1.0);
            return Result.Ok;
        }

        return Result.InvalidArgument;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static double NormalizedParamToPlain(void* self, uint id, double value) =>
        id == GainParameterId ? value * MaxGain : value * 100.0;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static double PlainParamToNormalized(void* self, uint id, double value) =>
        id == GainParameterId
            ? Math.Clamp(value / MaxGain, 0.0, 1.0)
            : Math.Clamp(value / 100.0, 0.0, 1.0);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static double GetParamNormalized(void* self, uint id)
    {
        var instance = Self<PluginComponent>(self);
        return id switch
        {
            GainParameterId => instance._gainNormalized,
            SidechainAmountParameterId => instance._sidechainAmount,
            _ => 0.0
        };
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetParamNormalized(void* self, uint id, double value)
    {
        var instance = Self<PluginComponent>(self);
        double clamped = Math.Clamp(value, 0.0, 1.0);

        if (id == GainParameterId)
        {
            instance._gainNormalized = clamped;
            return Result.Ok;
        }

        if (id == SidechainAmountParameterId)
        {
            instance._sidechainAmount = clamped;
            return Result.Ok;
        }

        return Result.InvalidArgument;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetComponentHandler(void* self, void* handler)
    {
        Self<PluginComponent>(self)._componentHandler = handler;
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void* CreateView(void* self, byte* name) => null;
}
