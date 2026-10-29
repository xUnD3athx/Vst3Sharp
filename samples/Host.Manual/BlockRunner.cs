using Vst3.Interop;

namespace Host.Manual;

internal static unsafe class BlockRunner
{
    public const int Frames = 128;

    public static bool Run(
        delegate* unmanaged[Stdcall]<void*, ProcessData*, int> process,
        void* processor,
        float input,
        float expected,
        void* events = null,
        bool withSidechain = false,
        float sidechain = 0.0f,
        void* changes = null)
    {
        float[] inLeft = new float[Frames];
        float[] inRight = new float[Frames];
        float[] outLeft = new float[Frames];
        float[] outRight = new float[Frames];
        float[] sideLeft = new float[Frames];
        float[] sideRight = new float[Frames];

        Array.Fill(inLeft, input);
        Array.Fill(inRight, input);
        Array.Fill(sideLeft, sidechain);
        Array.Fill(sideRight, sidechain);

        fixed (float* pInLeft = inLeft, pInRight = inRight, pOutLeft = outLeft, pOutRight = outRight,
               pSideLeft = sideLeft, pSideRight = sideRight)
        {
            float** inChannels = stackalloc float*[2];
            inChannels[0] = pInLeft;
            inChannels[1] = pInRight;

            float** outChannels = stackalloc float*[2];
            outChannels[0] = pOutLeft;
            outChannels[1] = pOutRight;

            float** sideChannels = stackalloc float*[2];
            sideChannels[0] = pSideLeft;
            sideChannels[1] = pSideRight;

            AudioBusBuffers* buses = stackalloc AudioBusBuffers[2];

            AudioBusBuffers inputBus = default;
            inputBus.NumChannels = 2;
            inputBus.ChannelBuffers32 = inChannels;
            buses[0] = inputBus;

            if (withSidechain)
            {
                AudioBusBuffers sideBus = default;
                sideBus.NumChannels = 2;
                sideBus.ChannelBuffers32 = sideChannels;
                buses[1] = sideBus;
            }

            AudioBusBuffers outputBus = default;
            outputBus.NumChannels = 2;
            outputBus.ChannelBuffers32 = outChannels;

            ProcessData data = default;
            data.ProcessMode = ProcessModes.Realtime;
            data.SymbolicSampleSize = SampleSizes.Sample32;
            data.NumSamples = Frames;
            data.NumInputs = withSidechain ? 2 : 1;
            data.NumOutputs = 1;
            data.Inputs = buses;
            data.Outputs = &outputBus;
            data.InputEvents = events;
            data.InputParameterChanges = changes;

            int hr = process(processor, &data);

            bool pass = true;
            for (int i = 0; i < Frames; i++)
            {
                if (MathF.Abs(outLeft[i] - expected) > 1e-4f || MathF.Abs(outRight[i] - expected) > 1e-4f)
                {
                    pass = false;
                    break;
                }
            }

            Console.WriteLine(
                $"  process hr=0x{hr:X8} out={outLeft[0]:0.000} expected={expected:0.000} -> {(pass ? "PASS" : "FAIL")}");
            return pass;
        }
    }
}
