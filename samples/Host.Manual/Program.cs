using System.Runtime.InteropServices;
using Host.Manual;
using Vst3.Interop;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: Host.Manual <path-to-plugin.vst3>");
    return 1;
}

string path = Path.GetFullPath(args[0]);
if (!File.Exists(path))
{
    Console.Error.WriteLine($"not found: {path}");
    return 2;
}

unsafe
{
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
        var queryInterface = (delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)factoryVt[0];
        var releaseFactory = (delegate* unmanaged[Stdcall]<void*, uint>)factoryVt[2];
        var getFactoryInfo = (delegate* unmanaged[Stdcall]<void*, PFactoryInfo*, int>)factoryVt[3];
        var countClasses = (delegate* unmanaged[Stdcall]<void*, int>)factoryVt[4];
        var getClassInfo = (delegate* unmanaged[Stdcall]<void*, int, PClassInfo*, int>)factoryVt[5];
        var createInstance = (delegate* unmanaged[Stdcall]<void*, Guid*, Guid*, void**, int>)factoryVt[6];

        PFactoryInfo factoryInfo = default;
        int factoryHr = getFactoryInfo(factory, &factoryInfo);
        Console.WriteLine(
            $"getFactoryInfo -> 0x{factoryHr:X8} vendor=\"{factoryInfo.GetVendor()}\" url=\"{factoryInfo.GetUrl()}\" flags=0x{factoryInfo.Flags:X}");

        int classCount = countClasses(factory);
        Console.WriteLine($"countClasses -> {classCount}");
        if (classCount <= 0)
        {
            releaseFactory(factory);
            return 5;
        }

        PClassInfo classInfo = default;
        int classHr = getClassInfo(factory, 0, &classInfo);
        Console.WriteLine(
            $"[0] getClassInfo -> 0x{classHr:X8} cid={classInfo.Cid} category=\"{classInfo.GetCategory()}\" name=\"{classInfo.GetName()}\"");

        Guid factory2Iid = IIds.IPluginFactory2;
        void* factory2 = null;
        queryInterface(factory, &factory2Iid, &factory2);
        if (factory2 != null)
        {
            var getClassInfo2 = (delegate* unmanaged[Stdcall]<void*, int, PClassInfo2*, int>)Vtable.Slot(factory2, 7);
            PClassInfo2 classInfo2 = default;
            int hr2 = getClassInfo2(factory2, 0, &classInfo2);
            Console.WriteLine($"[0] getClassInfo2 -> 0x{hr2:X8} subCategories=\"{classInfo2.GetSubCategories()}\"");
            ((delegate* unmanaged[Stdcall]<void*, uint>)Vtable.Slot(factory2, 2))(factory2);
        }
        else
        {
            Console.WriteLine("IPluginFactory2 not supported");
        }

        Guid classId = classInfo.Cid;
        Guid componentIid = IIds.IComponent;
        void* component = null;
        int createHr = createInstance(factory, &classId, &componentIid, &component);
        Console.WriteLine($"createInstance(IComponent) -> 0x{createHr:X8} ptr=0x{(nint)component:X}");

        if (component != null)
        {
            nint* componentVt = Vtable.Of(component);
            var queryComponent = (delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)componentVt[0];
            var releaseComponent = (delegate* unmanaged[Stdcall]<void*, uint>)componentVt[2];
            var initialize = (delegate* unmanaged[Stdcall]<void*, void*, int>)componentVt[3];
            var terminate = (delegate* unmanaged[Stdcall]<void*, int>)componentVt[4];
            var getBusCount = (delegate* unmanaged[Stdcall]<void*, int, int, int>)componentVt[7];
            var getBusInfo = (delegate* unmanaged[Stdcall]<void*, int, int, int, BusInfo*, int>)componentVt[8];
            var activateBus = (delegate* unmanaged[Stdcall]<void*, int, int, int, byte, int>)componentVt[10];
            var setActive = (delegate* unmanaged[Stdcall]<void*, byte, int>)componentVt[11];
            var setState = (delegate* unmanaged[Stdcall]<void*, void*, int>)componentVt[12];
            var getState = (delegate* unmanaged[Stdcall]<void*, void*, int>)componentVt[13];

            Console.WriteLine($"initialize -> 0x{initialize(component, null):X8}");

            int audioIn = getBusCount(component, MediaTypes.Audio, BusDirections.Input);
            int audioOut = getBusCount(component, MediaTypes.Audio, BusDirections.Output);
            Console.WriteLine($"bus counts: audio in={audioIn} out={audioOut}");

            for (int i = 0; i < audioIn; i++)
            {
                BusInfo bus = default;
                int busHr = getBusInfo(component, MediaTypes.Audio, BusDirections.Input, i, &bus);
                Console.WriteLine(
                    $"  input[{i}] hr=0x{busHr:X8} channels={bus.ChannelCount} type={bus.BusType} flags=0x{bus.Flags:X} name=\"{bus.GetName()}\"");
            }

            Guid processorIid = IIds.IAudioProcessor;
            void* processor = null;
            queryComponent(component, &processorIid, &processor);

            Guid controllerIid = IIds.IEditController;
            void* controller = null;
            queryComponent(component, &controllerIid, &controller);

            Console.WriteLine($"QI: processor={(processor != null)} controller={(controller != null)}");

            if (processor != null && controller != null)
            {
                nint* processorVt = Vtable.Of(processor);
                var releaseProcessor = (delegate* unmanaged[Stdcall]<void*, uint>)processorVt[2];
                var setupProcessing = (delegate* unmanaged[Stdcall]<void*, ProcessSetup*, int>)processorVt[7];
                var setProcessing = (delegate* unmanaged[Stdcall]<void*, byte, int>)processorVt[8];
                var process = (delegate* unmanaged[Stdcall]<void*, ProcessData*, int>)processorVt[9];

                nint* controllerVt = Vtable.Of(controller);
                var releaseController = (delegate* unmanaged[Stdcall]<void*, uint>)controllerVt[2];
                var getParameterCount = (delegate* unmanaged[Stdcall]<void*, int>)controllerVt[8];
                var getParameterInfo = (delegate* unmanaged[Stdcall]<void*, int, ParameterInfo*, int>)controllerVt[9];
                var getParamStringByValue = (delegate* unmanaged[Stdcall]<void*, uint, double, char*, int>)controllerVt[10];
                var getParamNormalized = (delegate* unmanaged[Stdcall]<void*, uint, double>)controllerVt[14];
                var setParamNormalized = (delegate* unmanaged[Stdcall]<void*, uint, double, int>)controllerVt[15];

                int parameterCount = getParameterCount(controller);
                Console.WriteLine($"parameterCount -> {parameterCount}");

                if (parameterCount > 0)
                {
                    ParameterInfo parameter = default;
                    int paramHr = getParameterInfo(controller, 0, &parameter);
                    char* text = stackalloc char[128];
                    int textHr = getParamStringByValue(controller, 0, 0.25, text);
                    Console.WriteLine(
                        $"param[0] hr=0x{paramHr:X8} id={parameter.Id} default={parameter.DefaultNormalizedValue:0.000} text(0.25, hr=0x{textHr:X8})=\"{String128.Read(text)}\"");

                    ParameterInfo scAmount = default;
                    int scHr = getParameterInfo(controller, 1, &scAmount);
                    int scTextHr = getParamStringByValue(controller, 1, 0.8, text);
                    Console.WriteLine(
                        $"param[1] hr=0x{scHr:X8} id={scAmount.Id} default={scAmount.DefaultNormalizedValue:0.000} text(0.8, hr=0x{scTextHr:X8})=\"{String128.Read(text)}\"");
                }

                ProcessSetup setup = default;
                setup.ProcessMode = ProcessModes.Realtime;
                setup.SymbolicSampleSize = SampleSizes.Sample32;
                setup.MaxSamplesPerBlock = 128;
                setup.SampleRate = 48000.0;
                Console.WriteLine($"setupProcessing -> 0x{setupProcessing(processor, &setup):X8}");

                int setHr = setParamNormalized(controller, 0, 0.25);
                Console.WriteLine($"setParamNormalized(0, 0.25) -> 0x{setHr:X8} now={getParamNormalized(controller, 0):0.000}");

                setActive(component, 1);
                setProcessing(processor, 1);

                var events = NativeEventList.Create();

                Console.WriteLine("block 1: passthrough");
                BlockRunner.Run(process, processor, 0.8f, 0.4f);

                Console.WriteLine("block 1b: sidechain signal present, SC Amount still 0 (must stay unity)");
                BlockRunner.Run(process, processor, 0.8f, 0.4f, events: events.Pointer, withSidechain: true, sidechain: 0.8f);

                Event noteOn = default;
                noteOn.Type = EventTypes.NoteOn;
                noteOn.NoteOn.Channel = 0;
                noteOn.NoteOn.Pitch = 60;
                noteOn.NoteOn.Velocity = 0.5f;
                noteOn.NoteOn.NoteId = -1;
                events.Add(noteOn);
                Console.WriteLine("block 2: MIDI note-on velocity 0.5");
                BlockRunner.Run(process, processor, 0.8f, 0.2f, events: events.Pointer);

                events.Clear();
                Event noteOff = default;
                noteOff.Type = EventTypes.NoteOff;
                noteOff.NoteOff.Channel = 0;
                noteOff.NoteOff.Pitch = 60;
                noteOff.NoteOff.NoteId = -1;
                events.Add(noteOff);
                Console.WriteLine("block 3: MIDI note-off");
                BlockRunner.Run(process, processor, 0.8f, 0.4f, events: events.Pointer);

                events.Clear();
                activateBus(component, MediaTypes.Audio, BusDirections.Input, 1, 1);
                setParamNormalized(controller, 1, 0.8);
                Console.WriteLine("block 4: sidechain level 0.8, SC Amount 0.8");
                BlockRunner.Run(process, processor, 0.8f, 0.144f, events: events.Pointer, withSidechain: true, sidechain: 0.8f);

                Console.WriteLine("block 5: automation via IParameterChanges (gain 0.25)");
                setParamNormalized(controller, 1, 0.0);
                var queue = NativeParamValueQueue.Create(0, 0, 0.125);
                var changes = NativeParameterChanges.Create(queue.Pointer);
                BlockRunner.Run(process, processor, 0.8f, 0.2f, changes: changes.Pointer);

                Console.WriteLine("state roundtrip");
                setParamNormalized(controller, 0, 0.25);
                setParamNormalized(controller, 1, 0.8);
                var stream = NativeMemoryStream.Create();
                int saveHr = getState(component, stream.Pointer);
                stream.Rewind();
                setParamNormalized(controller, 0, 0.5);
                setParamNormalized(controller, 1, 0.0);
                int loadHr = setState(component, stream.Pointer);
                double gainAfter = getParamNormalized(controller, 0);
                double scAfter = getParamNormalized(controller, 1);
                bool statePass = Math.Abs(gainAfter - 0.25) < 1e-9 && Math.Abs(scAfter - 0.8) < 1e-9;
                Console.WriteLine(
                    $"getState=0x{saveHr:X8} setState=0x{loadHr:X8} gain={gainAfter:0.000} sc={scAfter:0.000} -> {(statePass ? "PASS" : "FAIL")}");

                events.Release();

                setProcessing(processor, 0);
                setActive(component, 0);
                releaseController(controller);
                releaseProcessor(processor);
            }

            terminate(component);
            releaseComponent(component);
        }

        releaseFactory(factory);

        if (NativeLibrary.TryGetExport(library, "ExitDll", out nint exitDll))
            Console.WriteLine($"ExitDll -> {((delegate* unmanaged[Stdcall]<byte>)exitDll)()}");
    }
    finally
    {
        NativeLibrary.Free(library);
    }
}

return 0;
