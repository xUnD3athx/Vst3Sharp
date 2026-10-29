using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vst3.Interop;

namespace Vst3.Plugin;

/// <summary>
/// Combined component / audio processor / edit controller (VST3 single component effect).
/// Three vtable views share one managed instance and one reference count.
/// Parameter and DSP behaviour come from the <see cref="PluginDescriptor"/>.
/// </summary>
internal unsafe sealed class PluginComponent : ComObject
{
    private const uint StateMagic = 0x31565350; // "PSV1"
    private const int MaxEventsPerBlock = 128;

    private static nint* s_componentVTable;
    private static nint* s_processorVTable;
    private static nint* s_controllerVTable;

    private readonly PluginDescriptor _descriptor;
    private readonly ParameterCollection _parameters;
    private readonly AudioProcessor _processor;
    private readonly Event[] _events = new Event[MaxEventsPerBlock];

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

    public PluginComponent(PluginDescriptor descriptor)
    {
        _descriptor = descriptor;
        _parameters = new ParameterCollection(descriptor.Parameters ?? Array.Empty<ParameterDescriptor>());

        _processor = descriptor.CreateProcessor?.Invoke()
            ?? throw new InvalidOperationException($"Plugin '{descriptor.Name}' has no processor factory.");

        _processor.Parameters = _parameters;
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
    private static int Terminate(void* self)
    {
        Self<PluginComponent>(self)._processor.Terminate();
        return Result.Ok;
    }

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
    private static int ActivateBus(void* self, int mediaType, int direction, int index, byte state) => Result.Ok;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetActive(void* self, byte state)
    {
        Self<PluginComponent>(self)._processor.SetActive(state != 0);
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetState(void* self, void* stream)
    {
        if (stream == null)
            return Result.InvalidArgument;

        var instance = Self<PluginComponent>(self);

        if (ReadUInt32(stream) != StateMagic)
            return Result.False;

        uint count = ReadUInt32(stream);
        if (count != (uint)instance._parameters.Count)
            return Result.False;

        for (int i = 0; i < instance._parameters.Count; i++)
        {
            double value = ReadDouble(stream);
            instance._parameters.SetNormalized(i, value);
        }

        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetState(void* self, void* stream)
    {
        if (stream == null)
            return Result.InvalidArgument;

        var instance = Self<PluginComponent>(self);
        WriteUInt32(stream, StateMagic);
        WriteUInt32(stream, (uint)instance._parameters.Count);

        for (int i = 0; i < instance._parameters.Count; i++)
            WriteDouble(stream, instance._parameters.GetNormalized(i));

        return Result.Ok;
    }

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

        var processor = Self<PluginComponent>(self)._processor;
        processor.SampleRate = setup->SampleRate;
        processor.MaxBlockSize = setup->MaxSamplesPerBlock;
        processor.SetupProcessing(setup->SampleRate, setup->MaxSamplesPerBlock);
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetProcessing(void* self, byte state) => Result.Ok;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int Process(void* self, ProcessData* data)
    {
        if (data == null)
            return Result.InvalidArgument;

        var instance = Self<PluginComponent>(self);
        ApplyParameterChanges(data, instance);

        int eventCount = 0;
        fixed (Event* events = instance._events)
        {
            void* eventList = data->InputEvents;
            if (eventList != null)
            {
                nint* vtable = *(nint**)eventList;
                var getEventCount = (delegate* unmanaged[Stdcall]<void*, int>)vtable[3];
                var getEvent = (delegate* unmanaged[Stdcall]<void*, int, Event*, int>)vtable[4];

                int count = getEventCount(eventList);
                for (int i = 0; i < count && eventCount < MaxEventsPerBlock; i++)
                {
                    Event e = default;
                    if (getEvent(eventList, i, &e) == Result.Ok)
                        events[eventCount++] = e;
                }
            }

            var context = new ProcessContext
            {
                SampleCount = data->NumSamples,
                InputBusCount = data->NumInputs,
                OutputBusCount = data->NumOutputs,
                Inputs = data->Inputs,
                Outputs = data->Outputs,
                Events = events,
                EventCount = eventCount
            };

            instance._processor.Process(in context);
        }

        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static uint GetTailSamples(void* self) => 0;

    // ----- IEditController -----

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetComponentState(void* self, void* stream) => Result.NotImplemented;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetParameterCount(void* self) => Self<PluginComponent>(self)._parameters.Count;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetParameterInfo(void* self, int index, ParameterInfo* info)
    {
        var parameters = Self<PluginComponent>(self)._parameters;
        if (info == null || index < 0 || index >= parameters.Count)
            return Result.InvalidArgument;

        var descriptor = parameters.GetDescriptor(index);
        info->Id = descriptor.Id;
        info->SetTitle(descriptor.Title);
        info->SetShortTitle(descriptor.ShortTitle);
        info->SetUnits(descriptor.Units);
        info->StepCount = descriptor.StepCount;
        info->DefaultNormalizedValue = parameters.ToNormalized(index, descriptor.Default);
        info->UnitId = 0;
        info->Flags = descriptor.Flags;
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int GetParamStringByValue(void* self, uint id, double value, char* text)
    {
        if (text == null)
            return Result.InvalidArgument;

        var parameters = Self<PluginComponent>(self)._parameters;
        int index = parameters.IndexOf(id);
        if (index < 0)
            return Result.InvalidArgument;

        var descriptor = parameters.GetDescriptor(index);
        double plain = parameters.ToPlain(index, Math.Clamp(value, 0.0, 1.0));

        Span<char> buffer = stackalloc char[64];
        if (!plain.TryFormat(buffer, out int written, "0.###", CultureInfo.InvariantCulture))
            return Result.InternalError;

        string? units = descriptor.Units;
        if (!string.IsNullOrEmpty(units))
        {
            if (written < buffer.Length)
                buffer[written++] = ' ';

            foreach (char c in units)
            {
                if (written >= buffer.Length)
                    break;
                buffer[written++] = c;
            }
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

        var parameters = Self<PluginComponent>(self)._parameters;
        int index = parameters.IndexOf(id);
        if (index < 0)
            return Result.InvalidArgument;

        string source = String128.Read(text).TrimEnd('%', 'x', 'X', ' ');
        if (!double.TryParse(source, NumberStyles.Float, CultureInfo.InvariantCulture, out double plain))
            return Result.False;

        *value = parameters.ToNormalized(index, plain);
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static double NormalizedParamToPlain(void* self, uint id, double value)
    {
        var parameters = Self<PluginComponent>(self)._parameters;
        int index = parameters.IndexOf(id);
        return index >= 0 ? parameters.ToPlain(index, Math.Clamp(value, 0.0, 1.0)) : 0.0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static double PlainParamToNormalized(void* self, uint id, double value)
    {
        var parameters = Self<PluginComponent>(self)._parameters;
        int index = parameters.IndexOf(id);
        return index >= 0 ? parameters.ToNormalized(index, value) : 0.0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static double GetParamNormalized(void* self, uint id) =>
        Self<PluginComponent>(self)._parameters.GetNormalized(id);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetParamNormalized(void* self, uint id, double value)
    {
        var parameters = Self<PluginComponent>(self)._parameters;
        if (parameters.IndexOf(id) < 0)
            return Result.InvalidArgument;

        parameters.SetNormalized(id, value);
        return Result.Ok;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static int SetComponentHandler(void* self, void* handler) => Result.Ok;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static void* CreateView(void* self, byte* name) => null;

    // ----- helpers -----

    private static void ApplyParameterChanges(ProcessData* data, PluginComponent instance)
    {
        void* changes = data->InputParameterChanges;
        if (changes == null)
            return;

        nint* vtable = *(nint**)changes;
        var getParameterCount = (delegate* unmanaged[Stdcall]<void*, int>)vtable[3];
        var getParameterData = (delegate* unmanaged[Stdcall]<void*, int, void*>)vtable[4];

        int count = getParameterCount(changes);
        for (int i = 0; i < count; i++)
        {
            void* queue = getParameterData(changes, i);
            if (queue == null)
                continue;

            nint* queueVtable = *(nint**)queue;
            var getParameterId = (delegate* unmanaged[Stdcall]<void*, uint>)queueVtable[3];
            var getPointCount = (delegate* unmanaged[Stdcall]<void*, int>)queueVtable[4];
            var getPoint = (delegate* unmanaged[Stdcall]<void*, int, int*, double*, int>)queueVtable[5];

            int points = getPointCount(queue);
            if (points <= 0)
                continue;

            int sampleOffset = 0;
            double value = 0.0;
            if (getPoint(queue, points - 1, &sampleOffset, &value) != Result.Ok)
                continue;

            instance._parameters.SetNormalized(getParameterId(queue), value);
        }
    }

    private static uint ReadUInt32(void* stream)
    {
        uint value = 0;
        ReadStream(stream, &value, 4);
        return value;
    }

    private static double ReadDouble(void* stream)
    {
        double value = 0.0;
        ReadStream(stream, &value, 8);
        return value;
    }

    private static void WriteUInt32(void* stream, uint value)
    {
        WriteStream(stream, &value, 4);
    }

    private static void WriteDouble(void* stream, double value)
    {
        WriteStream(stream, &value, 8);
    }

    private static int ReadStream(void* stream, void* buffer, int bytes)
    {
        nint* vtable = *(nint**)stream;
        var read = (delegate* unmanaged[Stdcall]<void*, void*, int, int*, int>)vtable[3];
        int readBytes = 0;
        return read(stream, buffer, bytes, &readBytes) == Result.Ok ? readBytes : -1;
    }

    private static int WriteStream(void* stream, void* buffer, int bytes)
    {
        nint* vtable = *(nint**)stream;
        var write = (delegate* unmanaged[Stdcall]<void*, void*, int, int*, int>)vtable[4];
        int written = 0;
        return write(stream, buffer, bytes, &written) == Result.Ok ? written : -1;
    }
}
