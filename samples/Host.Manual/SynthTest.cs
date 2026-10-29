using System.Runtime.InteropServices;
using Vst3.Interop;

namespace Host.Manual;

/// <summary>Offline instrument test: note-on must produce audio, note-off must release it.</summary>
internal static unsafe class SynthTest
{
    public static int Run(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"not found: {path}");
            return 2;
        }

        nint library = NativeLibrary.Load(path);
        try
        {
            if (NativeLibrary.TryGetExport(library, "InitDll", out nint initDll))
                Console.WriteLine($"InitDll -> {((delegate* unmanaged[Stdcall]<byte>)initDll)()}");

            if (!NativeLibrary.TryGetExport(library, "GetPluginFactory", out nint getFactoryPtr))
            {
                Console.Error.WriteLine("GetPluginFactory is not exported");
                return 3;
            }

            void* factory = ((delegate* unmanaged[Stdcall]<void*>)getFactoryPtr)();
            if (factory == null)
            {
                Console.Error.WriteLine("GetPluginFactory returned null");
                return 4;
            }

            nint* factoryVt = Vtable.Of(factory);
            var releaseFactory = (delegate* unmanaged[Stdcall]<void*, uint>)factoryVt[2];
            var countClasses = (delegate* unmanaged[Stdcall]<void*, int>)factoryVt[4];
            var getClassInfo = (delegate* unmanaged[Stdcall]<void*, int, PClassInfo*, int>)factoryVt[5];
            var createInstance = (delegate* unmanaged[Stdcall]<void*, Guid*, Guid*, void**, int>)factoryVt[6];

            if (countClasses(factory) < 1)
            {
                Console.Error.WriteLine("factory exposes no classes");
                releaseFactory(factory);
                return 5;
            }

            PClassInfo classInfo = default;
            getClassInfo(factory, 0, &classInfo);
            Console.WriteLine($"class: \"{classInfo.GetName()}\" cid={classInfo.Cid}");

            Guid classId = classInfo.Cid;
            Guid componentIid = IIds.IComponent;
            void* component = null;
            createInstance(factory, &classId, &componentIid, &component);
            if (component == null)
            {
                Console.Error.WriteLine("createInstance failed");
                releaseFactory(factory);
                return 6;
            }

            nint* componentVt = Vtable.Of(component);
            var queryComponent = (delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)componentVt[0];
            var releaseComponent = (delegate* unmanaged[Stdcall]<void*, uint>)componentVt[2];
            var initialize = (delegate* unmanaged[Stdcall]<void*, void*, int>)componentVt[3];
            var terminate = (delegate* unmanaged[Stdcall]<void*, int>)componentVt[4];
            var getBusCount = (delegate* unmanaged[Stdcall]<void*, int, int, int>)componentVt[7];
            var setActive = (delegate* unmanaged[Stdcall]<void*, byte, int>)componentVt[11];

            initialize(component, null);

            int audioIn = getBusCount(component, MediaTypes.Audio, BusDirections.Input);
            int audioOut = getBusCount(component, MediaTypes.Audio, BusDirections.Output);
            int eventIn = getBusCount(component, MediaTypes.Event, BusDirections.Input);
            Console.WriteLine($"buses: audio in={audioIn} out={audioOut} event in={eventIn}");

            Guid processorIid = IIds.IAudioProcessor;
            void* processor = null;
            queryComponent(component, &processorIid, &processor);

            Guid controllerIid = IIds.IEditController;
            void* controller = null;
            queryComponent(component, &controllerIid, &controller);

            if (processor == null || controller == null)
            {
                Console.Error.WriteLine("processor/controller missing");
                relelease(component, terminate, releaseComponent);
                releaseFactory(factory);
                return 7;
            }

            nint* processorVt = Vtable.Of(processor);
            var releaseProcessor = (delegate* unmanaged[Stdcall]<void*, uint>)processorVt[2];
            var setupProcessing = (delegate* unmanaged[Stdcall]<void*, ProcessSetup*, int>)processorVt[7];
            var setProcessing = (delegate* unmanaged[Stdcall]<void*, byte, int>)processorVt[8];
            var process = (delegate* unmanaged[Stdcall]<void*, ProcessData*, int>)processorVt[9];

            nint* controllerVt = Vtable.Of(controller);
            var releaseController = (delegate* unmanaged[Stdcall]<void*, uint>)controllerVt[2];
            var getParameterCount = (delegate* unmanaged[Stdcall]<void*, int>)controllerVt[8];
            var getParameterInfo = (delegate* unmanaged[Stdcall]<void*, int, ParameterInfo*, int>)controllerVt[9];
            var setParamNormalized = (delegate* unmanaged[Stdcall]<void*, uint, double, int>)controllerVt[15];

            ProcessSetup setup = default;
            setup.ProcessMode = ProcessModes.Realtime;
            setup.SymbolicSampleSize = SampleSizes.Sample32;
            setup.MaxSamplesPerBlock = 128;
            setup.SampleRate = 48000.0;
            Console.WriteLine($"setupProcessing -> 0x{setupProcessing(processor, &setup):X8}");

            int parameterCount = getParameterCount(controller);
            Console.WriteLine($"parameterCount -> {parameterCount}");
            if (parameterCount > 0)
            {
                ParameterInfo parameter = default;
                getParameterInfo(controller, 0, &parameter);
                Console.WriteLine($"param[0] id={parameter.Id} default={parameter.DefaultNormalizedValue:0.000}");
                setParamNormalized(controller, 0, 0.8);
            }

            setActive(component, 1);
            setProcessing(processor, 1);

            var events = NativeEventList.Create();

            Event noteOn = default;
            noteOn.Type = EventTypes.NoteOn;
            noteOn.NoteOn.Channel = 0;
            noteOn.NoteOn.Pitch = 69;
            noteOn.NoteOn.Velocity = 0.8f;
            noteOn.NoteOn.NoteId = -1;
            events.Add(noteOn);

            float attackPeak = BlockRunner.RunInstrument(process, processor, events.Pointer);
            events.Clear();
            for (int i = 1; i < 8; i++)
                attackPeak = MathF.Max(attackPeak, BlockRunner.RunInstrument(process, processor, events.Pointer));

            bool noteOnPass = attackPeak > 0.05f;
            Console.WriteLine($"note-on produced audio (peak={attackPeak:0.000}) -> {(noteOnPass ? "PASS" : "FAIL")}");

            events.Clear();
            Event noteOff = default;
            noteOff.Type = EventTypes.NoteOff;
            noteOff.NoteOff.Channel = 0;
            noteOff.NoteOff.Pitch = 69;
            noteOff.NoteOff.NoteId = -1;
            events.Add(noteOff);

            float lastPeak = BlockRunner.RunInstrument(process, processor, events.Pointer);
            events.Clear();
            for (int i = 1; i < 40; i++)
                lastPeak = BlockRunner.RunInstrument(process, processor, events.Pointer);

            bool releasePass = lastPeak < 0.01f;
            Console.WriteLine($"note-off released audio (peak={lastPeak:0.000}) -> {(releasePass ? "PASS" : "FAIL")}");

            events.Release();
            setProcessing(processor, 0);
            setActive(component, 0);
            releaseController(controller);
            releaseProcessor(processor);
            terminate(component);
            releaseComponent(component);
            releaseFactory(factory);

            if (NativeLibrary.TryGetExport(library, "ExitDll", out nint exitDll))
                Console.WriteLine($"ExitDll -> {((delegate* unmanaged[Stdcall]<byte>)exitDll)()}");

            return noteOnPass && releasePass ? 0 : 8;
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }

    private static void relelease(void* component,
        delegate* unmanaged[Stdcall]<void*, int> terminate,
        delegate* unmanaged[Stdcall]<void*, uint> release)
    {
        terminate(component);
        release(component);
    }
}
