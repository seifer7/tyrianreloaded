using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LdsPlayerTests
{
    public static void Run(string? executable = null)
    {
        TestClipInt16();
        TestOplInitialization();
        TestOperatorTiming();
        TestDrumTiming();
        TestOperatorOutput();
        TestWaveformConfiguration();
        TestOperatorActivation();
        TestFrequencyConfiguration();
        TestOplRegisterWrites();
        TestOplSampleRendering();
        TestOplPorts();
        TestOff();
        TestAttackConfiguration();
        TestSustainLevelConfiguration();
        TestDecayReleaseConfiguration();
        TestSustainKeepConfiguration();
        TestVibratoConfiguration();
        TestFeedbackConfiguration();
        TestSustain();
        TestRelease();
        TestDecay();
        TestAttack();
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-lds-{Guid.NewGuid():N}.lds");
        try
        {
            for (var scenario = 0; scenario < 24; scenario++)
            {
                var fixture = Fixture(scenario);
                var output = Execute(fixture, scenario);
                if (executable is not null)
                {
                    File.WriteAllBytes(path, fixture);
                    CompareNative(executable, path, scenario, output);
                }
            }
            TestFailuresAndIntegration();
        }
        finally { File.Delete(path); }
    }

    private static void TestClipInt16()
    {
        int[] inputs = [int.MinValue, -32769, -32768, -1, 0, 1, 32767, 32768, int.MaxValue];
        short[] expected = [short.MinValue, short.MinValue, short.MinValue, -1, 0, 1, short.MaxValue, short.MaxValue, short.MaxValue];
        for (var i = 0; i < inputs.Length; i++)
            Check(OplSample.ClipInt16(inputs[i]) == expected[i], $"OPL sample clipping differs at {inputs[i]}.");
    }

    private static void TestOplInitialization()
    {
        const uint sampleRate = 44100;
        const double chipFrequency = 14318180.0 / 288.0;
        var opl2 = OplChipState.Initialize(sampleRate);
        Check(!opl2.IsOpl3 && opl2.Registers.Length == 256 &&
            opl2.WaveSelections.Length == 22 && opl2.Operators.Length == 18 &&
            opl2.Envelopes.Length == 18,
            "OPL2 initialization created the wrong register or operator state.");
        Check(opl2.GeneratorIncrement == (uint)(chipFrequency * 0x10000 / sampleRate) &&
            opl2.FrequencyTables.ReciprocalSampleRate == 1.0 / sampleRate &&
            opl2.VibratoIncrement == (uint)(16384 * chipFrequency / sampleRate) &&
            opl2.TremoloIncrement == (uint)(53 * 3.7 * 0x1000000 / sampleRate),
            "OPL clock or modulation increments differ from native initialization.");
        Check(opl2.VibratoTable.SequenceEqual(new[] { 8, 4, 0, -4, -8, -4, 0, 4 }) &&
            opl2.VibratoConstant.Length == 512 && opl2.VibratoConstant.All(value => value == 0) &&
            opl2.TremoloConstant.Length == 512 && opl2.TremoloConstant.All(value => value == 0x10000),
            "OPL modulation tables or constant buffers were not initialized correctly.");
        Check(opl2.TremoloTable.Length == 106 && opl2.TremoloTable[13] == 0x10000 &&
            opl2.TremoloTable[14] == 0x10000 && opl2.TremoloTable[66] == 0x10000,
            "OPL tremolo table size or zero-depth samples differ.");
        Check(opl2.Operators.All(op => op.Mode == OplOperatorMode.Off &&
            op.WaveformPosition == 0 && op.TimeIncrement == 0 && op.Volume == 0) &&
            opl2.Envelopes.All(envelope => envelope.Mode == OplOperatorMode.Off &&
                envelope.CurrentEnvelopeStep == 0 && envelope.ReleaseMultiplier == 1 &&
                envelope.DecayMultiplier == 1),
            "OPL operators or envelopes were not cleared to their native initial state.");
        Check(opl2.Registers.All(value => value == 0) && opl2.WaveSelections.All(value => value == 0) &&
            opl2.Status == 0 && opl2.RegisterIndex == 0,
            "OPL register, selection, or port state was not reset.");

        var opl3 = OplChipState.Initialize(sampleRate, opl3: true);
        Check(opl3.IsOpl3 && opl3.Registers.Length == 512 &&
            opl3.WaveSelections.Length == 44 && opl3.Operators.Length == 36 &&
            opl3.Envelopes.Length == 36,
            "OPL3 initialization created the wrong second-bank state.");
        Throws<ArgumentOutOfRangeException>(() => OplChipState.Initialize(0));
    }

    private static void TestOplPorts()
    {
        var opl2 = new OplPortState { Status = 0xA1 };
        Check(opl2.Read(0) == 0xA7 && opl2.Read(2) == 0xA7 &&
            opl2.Read(1) == byte.MaxValue && opl2.Read(3) == byte.MaxValue,
            "OPL2 status and detection-port values differ.");
        foreach (var port in new uint[] { 0, 1, 2, 3 })
        {
            opl2.WriteIndex(port, 5);
            Check(opl2.RegisterIndex == 5, $"OPL2 index write at port {port} used the wrong bank.");
        }

        var opl3 = new OplPortState(opl3: true) { Status = 0xA1 };
        Check(opl3.Read(0) == 0xA1 && opl3.Read(2) == 0xA1 &&
            opl3.Read(1) == 0 && opl3.Read(3) == 0,
            "OPL3 status and detection-port values differ.");
        opl3.WriteIndex(1, 4);
        Check(opl3.RegisterIndex == 4, "Disabled OPL3 mode unexpectedly selected the second bank.");
        opl3.WriteIndex(3, 5);
        Check(opl3.RegisterIndex == 0x105, "Writing register 5 on the second OPL3 port did not select the second bank.");
        opl3.WriteRegister(0x105, 1);
        opl3.WriteIndex(1, 4);
        Check(opl3.RegisterIndex == 0x104, "Enabled OPL3 mode did not select the second bank.");
        opl3.WriteRegister(0x105, 0);
        opl3.WriteIndex(1, 4);
        Check(opl3.RegisterIndex == 4, "Disabled OPL3 mode retained the second-bank selection.");
        opl3.WriteIndex(0, 4);
        Check(opl3.RegisterIndex == 4, "Primary OPL3 port selected the second bank.");
    }

    private static void TestOplRegisterWrites()
    {
        var opl2 = OplChipState.Initialize(44100);
        opl2.WriteRegister(0x20, 0x61);
        opl2.WriteRegister(0x23, 0x21);
        opl2.WriteRegister(0x40, 0x08);
        opl2.WriteRegister(0x43, 0x04);
        opl2.WriteRegister(0x60, 0xf3);
        opl2.WriteRegister(0x63, 0xe2);
        opl2.WriteRegister(0x80, 0x74);
        opl2.WriteRegister(0x83, 0x65);
        opl2.WriteRegister(0xa0, 0x80);
        opl2.WriteRegister(0xb0, 0x2d);
        Check(opl2.Operators[0].TimeIncrement != 0 && opl2.Operators[9].TimeIncrement != 0 &&
        opl2.Operators[0].Volume > 0 && opl2.Operators[9].Volume > 0,
        "OPL channel frequency writes did not update both operators.");
        Check(opl2.Envelopes[0].Attack0 != 0 && opl2.Envelopes[9].Attack0 != 0 &&
        opl2.Envelopes[0].SustainLevel > 0 && opl2.Envelopes[9].SustainLevel > 0,
        "OPL envelope register writes did not update mapped operator parameters.");
        Check(opl2.Operators[0].ActivityState == 1 && opl2.Operators[9].ActivityState == 1 &&
        opl2.Operators[0].Mode == OplOperatorMode.Attack &&
        opl2.Envelopes[0].Mode == OplOperatorMode.Attack,
        "OPL key-on did not activate the mapped modulator and carrier.");
        opl2.WriteRegister(0xb0, 0x0d);
        Check(opl2.Operators[0].ActivityState == 0 && opl2.Operators[9].ActivityState == 0 &&
        opl2.Operators[0].Mode == OplOperatorMode.Release &&
        opl2.Envelopes[0].Mode == OplOperatorMode.Release,
        "OPL key-off did not move the mapped operators to release.");

        opl2.WriteRegister(0x01, 0x20);
        opl2.WriteRegister(0xe0, 3);
        Check(opl2.WaveSelections[0] == 3 && opl2.Operators[0].WaveformStart == OplWaveforms.GetStart(3),
        "OPL2 waveform selection did not honor the waveform-enable register.");
        opl2.WriteRegister(0xa6, 0x80);
        opl2.WriteRegister(0xb6, 0x2d);
        opl2.WriteRegister(0xa7, 0x40);
        opl2.WriteRegister(0xb7, 0x1d);
        opl2.WriteRegister(0xa8, 0xc0);
        opl2.WriteRegister(0xb8, 0x25);
        opl2.WriteRegister(0xb6, 0x0d);
        opl2.WriteRegister(0xb8, 0x05);
        opl2.WriteRegister(0xbd, 0x3f);
        foreach (var index in new[] { 6, 7, 8, 15, 16, 17 })
        Check(opl2.Operators[index].ActivityState == 2,
            $"OPL percussion register did not activate operator {index}.");
        Check(opl2.Operators[6].TimeIncrement != 0 && opl2.Operators[15].TimeIncrement != 0 &&
        opl2.Operators[16].TimeIncrement != 0 && opl2.Operators[8].TimeIncrement != 0 &&
        opl2.Operators[17].TimeIncrement != 0 && opl2.Operators[7].TimeIncrement != 0,
        "OPL percussion channels did not apply their channel-specific frequency registers.");
        opl2.WriteRegister(0xbd, 0);
        foreach (var index in new[] { 6, 7, 8, 15, 16, 17 })
        Check(opl2.Operators[index].ActivityState == 0,
            $"OPL percussion key-off did not disable operator {index}.");
        Throws<ArgumentOutOfRangeException>(() => opl2.WriteRegister(0x100, 0));

        var opl3 = OplChipState.Initialize(44100, opl3: true);
        opl3.WriteRegister(0x105, 1);
        opl3.WriteRegister(0x104, 0x09);
        Check(opl3.Operators[0].IsFourOperator && opl3.Operators[3].IsFourOperatorAttached &&
            opl3.Operators[18].IsFourOperator && opl3.Operators[21].IsFourOperatorAttached &&
            !opl3.Operators[1].IsFourOperator && !opl3.Operators[4].IsFourOperatorAttached,
            "OPL3 four-operator enable bits did not map to their channel pairs.");
        opl3.WriteRegister(0x104, 0);
        Check(!opl3.Operators[0].IsFourOperator && !opl3.Operators[3].IsFourOperatorAttached &&
            !opl3.Operators[18].IsFourOperator && !opl3.Operators[21].IsFourOperatorAttached,
            "OPL3 four-operator disable bits did not clear channel-pair state.");
        opl3.WriteRegister(0x120, 0x40);
        opl3.WriteRegister(0x1a0, 0x80);
        opl3.WriteRegister(0x1b0, 0x2d);
        Check(opl3.Operators[18].ActivityState == 1 && opl3.Operators[27].ActivityState == 1 &&
        opl3.Operators[18].Vibrato,
        "OPL3 second-bank register writes did not route to the second operator set.");
        opl3.WriteRegister(0x1c0, 0x30);
        Check(opl3.Operators[18].LeftPan == 1 && opl3.Operators[18].RightPan == 1,
        "OPL3 channel register did not update stereo panning.");
        opl3.WriteRegister(0xe0, 7);
        Check(opl3.WaveSelections[0] == 7,
        "OPL3 mode did not expose all eight waveform selections.");
        opl3.WriteRegister(0x105, 0);
        opl3.WriteRegister(0x1e0, 7);
        Check(opl3.WaveSelections[22] == 3,
        "OPL3 mode-off second-bank waveform selection did not restrict to four waveforms.");
    }

    private static void TestOplSampleRendering()
    {
        int[] expected =
        [
        0, 3680, 3213, -872, -3973, -2694, 2084, 4092, 1841, -2254, -4092, -1497, 2358, 4095, 2399, -1164,
        -3774, -3447, -725, 2317, 4001, 3600, 1213, -1332, -3274, -4080, -3734, -2337, -774, 774, 2062, 2983,
        3563, 3926, 4051, 4092, 4095, 4095, 4093, 4062, 3954, 3691, 3182, 2233, 1019, -476, -2062, -3391,
        -4073, -3576, -1908, 526, 2860, 4071, 3101, 426, -2578, -4084, -2949, 774, 3624, 3723, 823, -2787
        ];
        var opl = OplChipState.Initialize(44100);
        opl.WriteRegister(0x01, 0x20);
        opl.WriteRegister(0x20, 0x01);
        opl.WriteRegister(0x23, 0x01);
        opl.WriteRegister(0x40, 0x00);
        opl.WriteRegister(0x43, 0x00);
        opl.WriteRegister(0x60, 0xf0);
        opl.WriteRegister(0x63, 0xf0);
        opl.WriteRegister(0x80, 0x04);
        opl.WriteRegister(0x83, 0x04);
        opl.WriteRegister(0xa0, 0x98);
        opl.WriteRegister(0xb0, 0x31);
        opl.WriteRegister(0xc0, 0x00);
        short[] actual = new short[expected.Length];
        opl.Render(actual);
        var actualIntegers = actual.Select(value => (int)value).ToArray();
        var firstDifference = Enumerable.Range(0, expected.Length)
        .FirstOrDefault(index => actualIntegers[index] != expected[index], -1);
        Check(firstDifference < 0,
        $"Rendered OPL2 FM samples differ from the native sample oracle at {firstDifference}: " +
        (firstDifference < 0 ? "" : $"{actualIntegers[firstDifference]} != {expected[firstDifference]}; " +
         $"actual [{string.Join(",", actualIntegers.Take(12))}], expected [{string.Join(",", expected.Take(12))}]; " +
         $"operator={opl.Operators[0].TimeIncrement}/{opl.Operators[0].TimeCount}/{opl.Operators[0].WaveformPosition}; " +
         $"carrier={opl.Operators[9].TimeIncrement}/{opl.Operators[9].TimeCount}/{opl.Operators[9].WaveformPosition}; " +
         $"step={opl.Operators[9].StepAmplitude}, volume={opl.Operators[9].Volume}."));
        Check(RenderOplSignature(1, 64) == 0x0ab84efd957e47feUL,
            "Additive OPL2 sample output differs from the native oracle.");
        Check(RenderOplSignature(2, 64) == 0x91885a690bcb434dUL,
            "Tremolo-enabled OPL2 sample output differs from the native oracle.");
        Check(RenderOplSignature(3, 600) == 0xb5c5d06bf5240306UL,
            "OPL2 block-boundary sample output differs from the native oracle.");
        Check(RenderOplSignature(4, 64) == 0x71bd600459088d7dUL,
            "Mixed OPL2 channel output differs from the native oracle.");
        Check(RenderOplSignature(6, 64) == 0x80dbd232dd817bd2UL,
            "OPL2 additive feedback output differs from the native oracle.");
        ulong[] fourOperatorExpected = [
            0x20f829d541fe5b9dUL, 0x665f84aedf543c41UL,
            0x14964fc9cb8de3d5UL, 0xf5001e3ccd20485dUL
        ];
        ulong[] fourOperatorBlockExpected = [
            0x4ccdaf3a6a1e59bdUL, 0x630bb7253daf0b2dUL,
            0xdd2a56adba432021UL, 0x321307250ce3f651UL
        ];
        for (byte algorithm = 0; algorithm < 4; algorithm++)
        {
            var signature = RenderOpl3Signature(algorithm, 64);
            Check(signature == fourOperatorExpected[algorithm],
                $"OPL3 four-operator algorithm {algorithm} sample output differs from native: {signature:x16}.");
            signature = RenderOpl3Signature(algorithm, 600);
            Check(signature == fourOperatorBlockExpected[algorithm],
                $"OPL3 four-operator algorithm {algorithm} block-boundary output differs from native: {signature:x16}.");
        }
        var randomCalls = 0;
        Check(RenderOplRhythmSignature(() => { randomCalls++; return 1; }) == 0x93e7cfe0af62bf37UL &&
            randomCalls == 64,
            "OPL2 percussion samples or per-sample noise reads differ from the native oracle.");

        var resettable = OplChipState.Initialize(44100);
        ConfigureVoice(resettable, 0, additive: false, tremolo: false, frequency: 0x98);
        IOplSynthesizer synthesizer = resettable;
        synthesizer.Reset();
        short[] resetSamples = new short[64];
        synthesizer.Render(resetSamples);
        Check(resettable.Registers.All(value => value == 0) &&
            resetSamples.All(value => value == 0),
            "The OPL synthesizer reset contract did not clear registers and rendered output.");
    }

    private static ulong RenderOplSignature(int scenario, int sampleCount)
    {
        var opl = OplChipState.Initialize(44100);
        opl.WriteRegister(0x01, 0x20);
        ConfigureVoice(opl, 0, scenario == 1, scenario == 2, 0x98);
        if (scenario == 4)
            ConfigureVoice(opl, 1, additive: true, tremolo: false, frequency: 0xb0);
        if (scenario == 6)
            opl.WriteRegister(0xc0, 0x0f);

        short[] samples = new short[sampleCount];
        opl.Render(samples);
        ulong hash = 14695981039346656037;
        foreach (var sample in samples)
        {
            var value = unchecked((ushort)sample);
            hash = unchecked((hash ^ (byte)value) * 1099511628211);
            hash = unchecked((hash ^ (byte)(value >> 8)) * 1099511628211);
        }
        return hash;
    }

    private static ulong RenderOplRhythmSignature(Func<uint> nextRandom)
    {
        var opl = OplChipState.Initialize(44100);
        opl.WriteRegister(0x01, 0x20);
        ConfigureVoice(opl, 6, additive: false, tremolo: false, frequency: 0x98);
        ConfigureVoice(opl, 7, additive: false, tremolo: false, frequency: 0x70);
        ConfigureVoice(opl, 8, additive: false, tremolo: false, frequency: 0xc0);
        opl.WriteRegister(0xb6, 0x10);
        opl.WriteRegister(0xb7, 0x10);
        opl.WriteRegister(0xb8, 0x10);
        opl.WriteRegister(0xbd, 0x3f);
        short[] samples = new short[64];
        opl.Render(samples, nextRandom);
        ulong hash = 14695981039346656037;
        foreach (var sample in samples)
        {
            var value = unchecked((ushort)sample);
            hash = unchecked((hash ^ (byte)value) * 1099511628211);
            hash = unchecked((hash ^ (byte)(value >> 8)) * 1099511628211);
        }
        return hash;
    }

    private static ulong RenderOpl3Signature(byte algorithm, int sampleCount)
    {
        var opl = OplChipState.Initialize(44100, opl3: true);
        opl.WriteRegister(0x105, 1);
        opl.WriteRegister(0x104, 1);
        opl.WriteRegister(0x01, 0x20);
        foreach (var register in new[] { 0x20, 0x23, 0x28, 0x2b })
            opl.WriteRegister((uint)register, 0x01);
        foreach (var register in new[] { 0x40, 0x43, 0x48, 0x4b })
            opl.WriteRegister((uint)register, 0);
        foreach (var register in new[] { 0x60, 0x63, 0x68, 0x6b })
            opl.WriteRegister((uint)register, 0xf0);
        foreach (var register in new[] { 0x80, 0x83, 0x88, 0x8b })
            opl.WriteRegister((uint)register, 0x04);
        opl.WriteRegister(0xa0, 0x98);
        opl.WriteRegister(0xb0, 0x31);
        opl.WriteRegister(0xc0, (byte)(0x30 | ((algorithm >> 1) & 1)));
        opl.WriteRegister(0xc3, (byte)(0x30 | (algorithm & 1)));

        short[] samples = new short[sampleCount * 2];
        opl.Render(samples);
        ulong hash = 14695981039346656037;
        foreach (var sample in samples)
        {
            var value = unchecked((ushort)sample);
            hash = unchecked((hash ^ (byte)value) * 1099511628211);
            hash = unchecked((hash ^ (byte)(value >> 8)) * 1099511628211);
        }
        return hash;
    }

    private static void ConfigureVoice(OplChipState opl, int channel, bool additive, bool tremolo, int frequency)
    {
        var modulatorRegister = channel < 3 ? channel : channel < 6 ? channel + 5 : channel + 10;
        var carrierRegister = modulatorRegister + 3;
        var modulationFlags = (byte)(1 | (tremolo ? 0x80 : 0));
        opl.WriteRegister((uint)(0x20 + modulatorRegister), modulationFlags);
        opl.WriteRegister((uint)(0x20 + carrierRegister), modulationFlags);
        opl.WriteRegister((uint)(0x40 + modulatorRegister), 0);
        opl.WriteRegister((uint)(0x40 + carrierRegister), 0);
        opl.WriteRegister((uint)(0x60 + modulatorRegister), 0xf0);
        opl.WriteRegister((uint)(0x60 + carrierRegister), 0xf0);
        opl.WriteRegister((uint)(0x80 + modulatorRegister), 4);
        opl.WriteRegister((uint)(0x80 + carrierRegister), 4);
        opl.WriteRegister((uint)(0xa0 + channel), (byte)frequency);
        opl.WriteRegister((uint)(0xb0 + channel), (byte)(0x30 | ((frequency >> 8) & 3)));
        opl.WriteRegister((uint)(0xc0 + channel), (byte)(additive ? 1 : 0));
    }

    private static void TestOperatorTiming()
    {
        var operatorState = new OplOperatorTimingState
        {
            TimeCount = 0x10001,
            TimeIncrement = 0x10000,
            GeneratorPosition = 0xF0000000
        };
        operatorState.Advance(-32768, 0x20000000);
        Check(operatorState.WaveformPosition == 0x10001 &&
            operatorState.TimeCount == 0x18001 &&
            operatorState.GeneratorPosition == 0x10000000,
            "Operator timing did not preserve native waveform capture, vibrato, or unsigned wraparound.");

        operatorState.TimeCount = 0;
        operatorState.TimeIncrement = 1;
        operatorState.Advance(-1, 0);
        Check(operatorState.TimeCount == 1,
            "Operator vibrato division did not truncate a negative sub-step toward zero.");
    }

    private static void TestDrumTiming()
    {
        var hihat = new OplOperatorTimingState { TimeCount = 0x88_0000, TimeIncrement = 0x10000 };
        var snare = new OplOperatorTimingState { TimeCount = 0x10000, TimeIncrement = 0x20000 };
        var cymbal = new OplOperatorTimingState { TimeCount = 0, TimeIncrement = 0x30000 };
        var randomCalls = 0;
        OplOperatorTimingState.AdvanceDrums(hihat, 0, snare, 0, cymbal, 0, 0x10000, () =>
        {
            randomCalls++;
            return 3;
        });
        Check(randomCalls == 1 && hihat.WaveformPosition == 0x2340000 &&
            snare.WaveformPosition == 0 && cymbal.WaveformPosition == 0x3000000 &&
            hihat.TimeCount == 0x890000 && snare.TimeCount == 0x30000 &&
            cymbal.TimeCount == 0x30000 &&
            hihat.GeneratorPosition == 0x10000 && snare.GeneratorPosition == 0x10000 &&
            cymbal.GeneratorPosition == 0x10000,
            "Drum operator phase, noise, waveform timing, or generator advancement differs.");
    }

    private static void TestOperatorOutput()
    {
        var operatorState = new OplOperatorTimingState
        {
            WaveformPosition = 0x10000,
            Waveform = [100, -200, 300, 400],
            WaveformMask = 3,
            StepAmplitude = 0.5,
            Volume = 0.25,
            Mode = OplOperatorMode.Sustain,
            Output = 11,
            LastOutput = 7
        };
        operatorState.Render(0x10000, 0x10000);
        Check(operatorState.Output == 153600 && operatorState.LastOutput == 11,
            "Operator output did not select the modulated waveform sample or preserve feedback output.");

        operatorState.WaveformPosition = 0x20000;
        operatorState.Render(-0x10000, 0x10000);
        Check(operatorState.Output == -102400 && operatorState.LastOutput == 153600,
            "Operator output did not wrap and mask a negative modulator index.");

        operatorState.Mode = OplOperatorMode.Off;
        operatorState.Output = 23;
        operatorState.LastOutput = 29;
        operatorState.Render(0, 0x10000);
        Check(operatorState.Output == 23 && operatorState.LastOutput == 29,
            "Off-state operator output changed output or feedback state.");
    }

    private static void TestWaveformConfiguration()
    {
        uint[] starts = [1024, 512, 1024, 768, 0, 0, 1280, 2048];
        uint[] masks = [1023, 1023, 511, 511, 1023, 767, 511, 1023];
        var operatorState = new OplOperatorTimingState();
        for (byte selection = 0; selection < 8; selection++)
        {
            operatorState.ConfigureWaveform(selection);
            Check(operatorState.WaveformStart == starts[selection] &&
                operatorState.WaveformMask == masks[selection] &&
                operatorState.Waveform.Length == 3072,
                $"Waveform selection {selection} used the wrong table range or mask.");
        }

        operatorState.ConfigureWaveform(0);
        Check(operatorState.Waveform[operatorState.WaveformStart] == 0 &&
            operatorState.Waveform[operatorState.WaveformStart + 1] == 100 &&
            operatorState.Waveform[operatorState.WaveformStart + 2] == 201,
            "Generated sine waveform samples differ from the native table.");
        operatorState.ConfigureWaveform(7);
        Check(operatorState.Waveform[operatorState.WaveformStart] == 0 &&
            operatorState.Waveform[operatorState.WaveformStart + 127] < 0,
            "Generated waveform table did not preserve the native clipped-waveform regions.");
    }

    private static void TestOperatorActivation()
    {
        uint[] initialPositions = [0, 512, 0, 256, 0, 0, 0, 128];
        for (byte selection = 0; selection < 8; selection++)
        {
            var operatorState = new OplOperatorTimingState { Mode = OplOperatorMode.Off };
            operatorState.Enable(selection, 1);
            Check(operatorState.TimeCount == initialPositions[selection] * 0x10000 &&
                operatorState.ActivityState == 1 && operatorState.Mode == OplOperatorMode.Attack,
                $"Enabling with waveform selection {selection} did not initialize the operator.");
        }

        var shared = new OplOperatorTimingState { TimeCount = 123, Mode = OplOperatorMode.Off };
        shared.Enable(0, 1);
        shared.Enable(3, 2);
        Check(shared.TimeCount == 0 && shared.ActivityState == 1 && shared.Mode == OplOperatorMode.Attack,
            "An already-active operator was reset by another activation.");
        shared.Disable(1);
        Check(shared.ActivityState == 0 && shared.Mode == OplOperatorMode.Release,
            "Disabling the final activity did not enter release.");

        shared.Enable(0, 3);
        shared.Disable(1);
        Check(shared.ActivityState == 2 && shared.Mode == OplOperatorMode.Attack,
            "Disabling one activity released an operator with another active.");
        shared.Disable(2);
        Check(shared.ActivityState == 0 && shared.Mode == OplOperatorMode.Release,
            "Disabling the last shared activity did not enter release.");

        shared.Mode = OplOperatorMode.Off;
        shared.Enable(0, 1);
        shared.Mode = OplOperatorMode.Off;
        shared.Disable(1);
        Check(shared.Mode == OplOperatorMode.Off,
            "Disabling an already-off envelope changed its mode.");
    }

    private static void TestFrequencyConfiguration()
    {
        const double sampleRate = 49716;
        var tables = new OplFrequencyTables(sampleRate);
        var envelope = new OplEnvelopeState();
        var operatorState = new OplOperatorTimingState();
        var registers = new OplFrequencyRegisters(0xff, 0x1b, 0x40, 0x1f, 0xc1, 0xa5, 0xb6);
        operatorState.ConfigureFrequency(registers, tables, envelope, opl3: false);

        var fNumber = 1023u;
        var octave = 6u;
        var frequencyMultiplier = 15 * (14318180.0 / 288.0) / 1024 * 0x10000 / sampleRate;
        var expectedIncrement = (uint)((double)(fNumber << (int)octave) * frequencyMultiplier);
        var expectedVolume = Math.Pow(2, 49 * -0.125 - 14);
        Check(operatorState.FrequencyHigh == 7 && operatorState.TimeOffset == 13 &&
            operatorState.TimeIncrement == expectedIncrement && operatorState.Volume == expectedVolume,
            "Frequency, octave, KSR, multiplier, or key-scale volume calculation differs.");

        var expectedEnvelope = new OplEnvelopeState();
        expectedEnvelope.ConfigureAttack(registers.AttackDecay, 13, tables.ReciprocalSampleRate, opl3: false);
        expectedEnvelope.ConfigureDecayRate(registers.AttackDecay, 13, tables.ReciprocalSampleRate);
        expectedEnvelope.ConfigureReleaseRate(registers.SustainRelease, 13, tables.ReciprocalSampleRate);
        Check(envelope.Attack0 == expectedEnvelope.Attack0 &&
            envelope.Attack1 == expectedEnvelope.Attack1 &&
            envelope.Attack2 == expectedEnvelope.Attack2 &&
            envelope.Attack3 == expectedEnvelope.Attack3 &&
            envelope.AttackStepMask == expectedEnvelope.AttackStepMask &&
            envelope.AttackSkipMask == expectedEnvelope.AttackSkipMask &&
            envelope.DecayMultiplier == expectedEnvelope.DecayMultiplier &&
            envelope.DecayStepMask == expectedEnvelope.DecayStepMask &&
            envelope.ReleaseMultiplier == expectedEnvelope.ReleaseMultiplier &&
            envelope.ReleaseStepMask == expectedEnvelope.ReleaseStepMask,
            "Frequency changes did not recalculate the dependent envelope rates.");

        operatorState.ConfigureFrequency(
            new OplFrequencyRegisters(0x80, 0x3f, 0, 0x0e, 0x00, 0, 0), tables, envelope, opl3: false);
        Check(operatorState.FrequencyHigh == 7 && operatorState.TimeOffset == 3,
            "Disabled KSR or note-select frequency calculations used the wrong key offset.");
    }

    private static void TestSustain()
    {
        var envelope = new OplEnvelopeState
        {
            GeneratorPosition = 0x30005,
            CurrentEnvelopeStep = 5
        };
        envelope.AdvanceSustain();
        Check(envelope.CurrentEnvelopeStep == 8 && envelope.GeneratorPosition == 5,
            "Sustain advancement did not consume whole samples while retaining the fractional position.");
        envelope.GeneratorPosition = 0xFFFF;
        envelope.AdvanceSustain();
        Check(envelope.CurrentEnvelopeStep == 8 && envelope.GeneratorPosition == 0xFFFF,
            "Sustain advancement consumed an incomplete fixed-point sample.");
    }

    private static void TestOff()
    {
        var envelope = new OplEnvelopeState
        {
            GeneratorPosition = 0x23456,
            CurrentEnvelopeStep = -7,
            Amplitude = 0.75,
            StepAmplitude = 0.25,
            ReleaseMultiplier = 0.5,
            DecayMultiplier = 0.6,
            SustainLevel = 0.1,
            Mode = OplOperatorMode.Off
        };
        envelope.AdvanceOff();
        Check(envelope.GeneratorPosition == 0x23456 && envelope.CurrentEnvelopeStep == -7 &&
            envelope.Amplitude == 0.75 && envelope.StepAmplitude == 0.25 &&
            envelope.ReleaseMultiplier == 0.5 && envelope.DecayMultiplier == 0.6 &&
            envelope.SustainLevel == 0.1 && envelope.Mode == OplOperatorMode.Off,
            "Off-state operator processing changed envelope state.");
    }

    private static void TestAttackConfiguration()
    {
        var envelope = new OplEnvelopeState();
        envelope.ConfigureAttack(0, 63, 0.5, opl3: false);
        Check(envelope.Attack0 == 0 && envelope.Attack1 == 1 && envelope.Attack2 == 0 &&
            envelope.Attack3 == 0 && envelope.AttackStepMask == 0 && envelope.AttackSkipMask == 0,
            "A disabled attack rate did not reset its polynomial and step masks.");

        envelope.ConfigureAttack(0x10, 0, 0.5, opl3: false);
        var frequency = 1.0 / 2.82624 * 0.5;
        Check(Math.Abs(envelope.Attack0 - 0.0377 * frequency) < 1e-15 &&
            Math.Abs(envelope.Attack1 - (10.73 * frequency + 1)) < 1e-15 &&
            Math.Abs(envelope.Attack2 + 17.57 * frequency) < 1e-15 &&
            Math.Abs(envelope.Attack3 - 7.42 * frequency) < 1e-15 &&
            envelope.AttackStepMask == 0x7ff && envelope.AttackSkipMask == 0xaa,
            "A normal attack rate produced different coefficients or envelope step masks.");

        envelope.ConfigureAttack(0xf0, 0, 0.5, opl3: true);
        Check(envelope.Attack0 == 2 && envelope.Attack1 == 0 &&
            envelope.Attack2 == 0 && envelope.Attack3 == 0,
            "OPL3 immediate attack threshold did not select the full-level transition polynomial.");
        envelope.ConfigureAttack(0xf0, 0, 0.5, opl3: false);
        Check(envelope.Attack0 != 2,
            "OPL2 immediate attack threshold was applied at the OPL3 boundary.");
        envelope.ConfigureAttack(0xf0, 2, 0.5, opl3: false);
        Check(envelope.Attack0 == 2 && envelope.Attack1 == 0 &&
            envelope.Attack2 == 0 && envelope.Attack3 == 0,
            "OPL2 immediate attack threshold did not activate at its native boundary.");
    }

    private static void TestSustainLevelConfiguration()
    {
        var envelope = new OplEnvelopeState();
        for (var sustain = 0; sustain <= 15; sustain++)
        {
            envelope.ConfigureSustainLevel((byte)(sustain << 4));
            var expected = sustain < 15 ? Math.Pow(2, sustain * -0.5) : 0;
            Check(envelope.SustainLevel == expected,
                $"Sustain level register value {sustain} mapped to a different output level.");
        }
        envelope.ConfigureSustainLevel(0x7f);
        Check(envelope.SustainLevel == Math.Pow(2, -3.5),
            "Sustain level configuration read low register bits instead of the high nibble.");
    }

    private static void TestDecayReleaseConfiguration()
    {
        ReadOnlySpan<double> rateConstants = [1.0 / 39.28064, 1.0 / 31.41608, 1.0 / 26.17344, 1.0 / 22.44608];
        const uint timeOffset = 5;
        const double reciprocalSampleRate = 0.01;
        var envelope = new OplEnvelopeState();
        for (var rate = 0; rate <= 15; rate++)
        {
            envelope.ConfigureDecayRate((byte)rate, timeOffset, reciprocalSampleRate);
            envelope.ConfigureReleaseRate((byte)rate, timeOffset, reciprocalSampleRate);
            var expectedMultiplier = 1.0;
            var expectedStepMask = (nint)0;
            if (rate != 0)
            {
                var frequency = -7.4493 * rateConstants[(int)(timeOffset & 3)] * reciprocalSampleRate;
                expectedMultiplier = Math.Pow(2, frequency * Math.Pow(2, rate + (timeOffset >> 2)));
                var steps = ((uint)rate * 4 + timeOffset) >> 2;
                expectedStepMask = (nint)((1u << (int)(steps <= 12 ? 12 - steps : 0)) - 1);
            }
            Check(Math.Abs(envelope.DecayMultiplier - expectedMultiplier) < 1e-15 &&
                Math.Abs(envelope.ReleaseMultiplier - expectedMultiplier) < 1e-15 &&
                envelope.DecayStepMask == expectedStepMask && envelope.ReleaseStepMask == expectedStepMask,
                $"Decay/release rate register value {rate} produced an incorrect multiplier or step mask.");
        }

        envelope.ConfigureDecayRate(0xf0, 0, reciprocalSampleRate);
        envelope.ConfigureReleaseRate(0xf0, 0, reciprocalSampleRate);
        Check(envelope.DecayMultiplier == 1 && envelope.DecayStepMask == 0 &&
            envelope.ReleaseMultiplier == 1 && envelope.ReleaseStepMask == 0,
            "Zero decay/release rate did not disable attenuation and clear the step mask.");
    }

    private static void TestSustainKeepConfiguration()
    {
        var envelope = new OplEnvelopeState { Mode = OplOperatorMode.Sustain };
        envelope.ConfigureSustainKeep(0x20);
        Check(envelope.SustainKeep && envelope.Mode == OplOperatorMode.Sustain,
            "Enabling sustain keep did not preserve the sustain state.");
        envelope.ConfigureSustainKeep(0);
        Check(!envelope.SustainKeep && envelope.Mode == OplOperatorMode.SustainNoKeep,
            "Disabling sustain keep did not transition to sustain-no-keep.");
        envelope.ConfigureSustainKeep(0x20);
        Check(envelope.SustainKeep && envelope.Mode == OplOperatorMode.Sustain,
            "Re-enabling sustain keep did not restore the sustain state.");
        envelope.Mode = OplOperatorMode.Release;
        envelope.ConfigureSustainKeep(0);
        Check(!envelope.SustainKeep && envelope.Mode == OplOperatorMode.Release,
            "Changing sustain keep altered an unrelated envelope mode.");
    }

    private static void TestVibratoConfiguration()
    {
        var operatorState = new OplOperatorTimingState();
        for (var register = 0; register <= byte.MaxValue; register++)
        {
            operatorState.ConfigureVibrato((byte)register);
            Check(operatorState.Vibrato == ((register & 0x40) != 0) &&
                operatorState.Tremolo == ((register & 0x80) != 0),
                $"Vibrato/tremolo register flags differed for 0x{register:X2}.");
        }
    }

    private static void TestFeedbackConfiguration()
    {
        var operatorState = new OplOperatorTimingState();
        for (var register = 0; register <= byte.MaxValue; register++)
        {
            operatorState.ConfigureFeedback((byte)register);
            var feedback = register & 14;
            var expected = feedback == 0 ? 0 : (int)Math.Pow(2, (feedback >> 1) + 8);
            Check(operatorState.FeedbackAmount == expected,
                $"Feedback amount differed for channel register value 0x{register:X2}.");
        }
    }

    private static void TestRelease()
    {
        var envelope = new OplEnvelopeState
        {
            GeneratorPosition = 0x30005,
            CurrentEnvelopeStep = 1,
            ReleaseStepMask = 1,
            Amplitude = 0.5,
            StepAmplitude = 0.25,
            ReleaseMultiplier = 0.5,
            Mode = OplOperatorMode.Release
        };
        envelope.AdvanceRelease();
        Check(envelope.Amplitude == 0.25 && envelope.StepAmplitude == 0.25 &&
            envelope.CurrentEnvelopeStep == 4 && envelope.GeneratorPosition == 5 &&
            envelope.Mode == OplOperatorMode.Release,
            "Release advancement changed the native envelope cadence or fractional position.");

        envelope.Amplitude = 0;
        envelope.StepAmplitude = 1;
        envelope.GeneratorPosition = 0x20000;
        envelope.CurrentEnvelopeStep = 0;
        envelope.ReleaseStepMask = 1;
        envelope.AdvanceRelease();
        Check(envelope.Amplitude == 0 && envelope.StepAmplitude == 0 &&
            envelope.Mode == OplOperatorMode.Off && envelope.GeneratorPosition == 0,
            "A silent release operator did not transition to off at its release-step boundary.");

        envelope.Mode = OplOperatorMode.SustainNoKeep;
        envelope.GeneratorPosition = 0x20000;
        envelope.AdvanceRelease();
        Check(envelope.Mode == OplOperatorMode.SustainNoKeep,
            "Release processing turned off an operator outside the release state.");

        envelope.Mode = OplOperatorMode.Release;
        envelope.Amplitude = 0.000000005;
        envelope.StepAmplitude = 1;
        envelope.GeneratorPosition = 0;
        envelope.AdvanceRelease();
        Check(envelope.Amplitude == 0.000000005 && envelope.StepAmplitude == 1,
            "Release processing changed a sub-threshold amplitude without a complete sample.");
    }

    private static void TestDecay()
    {
        var envelope = new OplEnvelopeState
        {
            GeneratorPosition = 0x30005,
            CurrentEnvelopeStep = 1,
            DecayStepMask = 1,
            Amplitude = 0.8,
            StepAmplitude = 0.1,
            DecayMultiplier = 0.5,
            SustainLevel = 0.5,
            SustainKeep = true,
            Mode = OplOperatorMode.Decay
        };
        envelope.AdvanceDecay();
        Check(envelope.Amplitude == 0.5 && envelope.StepAmplitude == 0.5 &&
            envelope.CurrentEnvelopeStep == 4 && envelope.GeneratorPosition == 5 &&
            envelope.Mode == OplOperatorMode.Sustain,
            "Sustain-keeping decay did not clamp the envelope at its sustain level.");

        envelope.Amplitude = 0.8;
        envelope.StepAmplitude = 0.1;
        envelope.CurrentEnvelopeStep = 2;
        envelope.GeneratorPosition = 0x20000;
        envelope.SustainKeep = false;
        envelope.AdvanceDecay();
        Check(envelope.Amplitude == 0.4 && envelope.StepAmplitude == 0.4 &&
            envelope.Mode == OplOperatorMode.SustainNoKeep,
            $"Non-keeping decay state mismatch: amplitude={envelope.Amplitude}, step={envelope.StepAmplitude}, mode={envelope.Mode}.");

        envelope.Amplitude = 0.75;
        envelope.StepAmplitude = 0.6;
        envelope.SustainLevel = 0.5;
        envelope.CurrentEnvelopeStep = 2;
        envelope.GeneratorPosition = 0x10000;
        envelope.AdvanceDecay();
        Check(envelope.Amplitude == 0.375 && envelope.StepAmplitude == 0.6 &&
            envelope.Mode == OplOperatorMode.SustainNoKeep,
            "Decay updated the step amplitude or sustain mode before an envelope-step boundary.");
    }

    private static void TestAttack()
    {
        var envelope = new OplEnvelopeState
        {
            GeneratorPosition = 0x30005,
            CurrentEnvelopeStep = 0,
            AttackStepMask = 0,
            AttackSkipMask = 2,
            AttackSkipPosition = 1,
            Amplitude = 0.5,
            StepAmplitude = 0.1,
            Attack1 = 1,
            Mode = OplOperatorMode.Attack
        };
        envelope.AdvanceAttack();
        Check(envelope.Amplitude == 0.5 && envelope.StepAmplitude == 0.5 &&
            envelope.AttackSkipPosition == 8 && envelope.CurrentEnvelopeStep == 3 &&
            envelope.GeneratorPosition == 5 && envelope.Mode == OplOperatorMode.Attack,
            "Attack advancement changed the native skip cadence or fractional position.");

        envelope.Amplitude = 0.5;
        envelope.Attack0 = 1.1;
        envelope.Attack1 = 0;
        envelope.GeneratorPosition = 0x10000;
        envelope.AttackStepMask = 0;
        envelope.AttackSkipMask = 0;
        envelope.AttackSkipPosition = 1;
        envelope.AdvanceAttack();
        Check(envelope.Amplitude == 1 && envelope.StepAmplitude == 1 &&
            envelope.Mode == OplOperatorMode.Decay && envelope.GeneratorPosition == 0,
            "Attack reaching full amplitude did not clamp and transition into decay.");
    }

    private static byte[] Fixture(int mode)
    {
        using var output = new MemoryStream();
        output.WriteByte((byte)(mode % 3)); BinaryFile.WriteUInt16(output, (ushort)(0x4300 + mode));
        output.WriteByte((byte)(mode % 3)); output.WriteByte(32);
        for (var i = 0; i < 9; i++) output.WriteByte((byte)((i + mode) % 4));
        output.WriteByte((byte)(mode * 7));
        BinaryFile.WriteUInt16(output, 64);
        for (var i = 0; i < 64; i++)
        {
            var patch = new byte[46];
            for (var j = 0; j < patch.Length; j++) patch[j] = unchecked((byte)(i * 31 + j * 19 + mode * 7));
            patch[10] = (byte)(i % 16);
            patch[11] = (byte)(i % 10); patch[12] = (byte)(i % 4);
            patch[13] = (byte)(i % 3 == 0 ? 0 : i % 3 == 1 ? 16 : 240);
            patch[14] = (byte)(i % 4 == 0 ? 0 : i % 4 == 1 ? 127 : i % 4 == 2 ? 128 : 255);
            patch[15] = (byte)(i % 4 == 0 ? 0 : i * 7); patch[16] = (byte)(i % 7);
            patch[19] = (byte)(i % 4 == 0 ? 0 : i);
            patch[20] = (byte)((i % 3 << 4) | (i % 5 == 4 ? 12 : i % 5));
            for (var j = 0; j < 12; j++) patch[21 + j] = (byte)(j % 5 == 0 ? 255 : j % 5 == 1 ? 128 : j);
            output.Write(patch);
        }
        BinaryFile.WriteUInt16(output, 4);
        for (var position = 0; position < 4; position++)
            for (var channel = 0; channel < 9; channel++)
            {
                BinaryFile.WriteUInt16(output, (ushort)((position * 9 + channel) * 32 * 2));
                byte[] transposes = [0, 1, 127, 64, 128, 129, 255, 192, 63];
                output.WriteByte(transposes[(channel + mode) % 9]);
            }
        BinaryFile.WriteUInt16(output, 0);
        for (var position = 0; position < 4; position++)
            for (var channel = 0; channel < 9; channel++)
                for (var row = 0; row < 32; row++)
                {
                    var note = (ushort)(((80 + (row + channel) % 40) << 8) | ((mode + channel + row) % 64));
                    var command = row switch
                    {
                        1 => 0xFD30,
                        3 => 0xF734,
                        4 => 0xF605,
                        6 => 0xF5F0,
                        8 => 0xF4C0,
                        9 => 0xF302,
                        10 => 0xF2FF,
                        11 => 0xFF20,
                        12 => 0xF800,
                        14 => 0xFB00,
                        15 => 0xFE00 | (mode % 3),
                        16 => 0xF101,
                        17 => 0xF001,
                        18 => 0xA101,
                        19 => 0x8100,
                        21 => 0x8002,
                        26 => mode % 4 == 0 ? (position == 3 ? 0xFC00 : 0xFA00) : 0xF900 | ((position + 1) % 4),
                        _ => note
                    };
                    // Break and stop commands affect the whole order; one channel
                    // issues them while the other eight still process their rows.
                    if (row == 26 && channel != 8) command = note;
                    BinaryFile.WriteUInt16(output, (ushort)command);
                }
        output.WriteByte(0x99);
        return output.ToArray();
    }

    internal static byte[] FixtureForAudio() => Fixture(1);

    private static string Execute(byte[] fixture, int scenario)
    {
        var output = new StringBuilder();
        var player = new LdsPlayer(new Opl(output));
        player.Load(fixture);
        State();
        for (var frame = 0; frame < 384; frame++)
        {
            output.Append($"frame:{frame}\n");
            if (frame == 128) player.Fade((byte)(scenario % 2 != 0 ? 129 : 7));
            if (frame == 256) player.Rewind();
            if (frame == 300)
            {
                player.SetRegister(0x40, 0x7F); player.SetRegister(0x40, 0x7F);
                player.SetRegisterMasked(0x40, 0xC0, 0x12);
            }
            var active = player.Advance();
            output.Append($"advance:{(active ? 1 : 0)}\n"); State();
        }
        output.Append("notes\n"); player.Rewind();
        for (var i = 0; i < 64; i++)
        {
            output.Append($"note:{i}\n");
            player.PlaySound(i, i % 9, 512 + i * 13);
            player.Update(); State();
        }
        player.Free();
        Check(!player.Playing && !player.Advance(), "Freed LDS player retained active playback.");
        return output.ToString();
        void State() => output.Append($"state:{(player.Playing ? 1 : 0)},{(player.SongLooped ? 1 : 0)},{player.Row},{player.PositionIndex},{player.Tempo},{player.TempoNow},{player.AllVolume},{player.FadeSpeed}\n");
    }

    private static void TestFailuresAndIntegration()
    {
        var output = new StringBuilder();
        var opl = new Opl(output);
        var player = new LdsPlayer(opl);
        var fixture = Fixture(1);
        foreach (var length in new[] { 0, 1, 16, 17, 50, 2962, 2970 })
            Throws<EndOfStreamException>(() => player.Load(fixture.AsSpan(0, length)));
        var malformed = fixture.ToArray(); malformed[0] = 3;
        Throws<InvalidDataException>(() => player.Load(malformed));
        player.Load(fixture);
        Throws<InvalidDataException>(() => player.PlaySound(64, 0, 512));
        Throws<InvalidDataException>(() => player.PlaySound(0, 9, 512));
        Throws<InvalidDataException>(() => player.PlaySound(0, 0, -1000));
        Throws<InvalidDataException>(() => player.SetRegister(255, 1));
        Throws<InvalidDataException>(() => player.SetRegisterMasked(255, 0, 1));
        var badPattern = fixture.ToArray();
        badPattern[17 + 64 * 46 + 2] = badPattern[17 + 64 * 46 + 3] = 255;
        player.Load(badPattern);
        for (var i = 0; i < 3; i++) player.Update();
        Throws<InvalidDataException>(() => player.Update());
        player.Load(fixture);
        var mixer = new AudioMixer(44100, player) { MusicStopped = false };
        var samples = new short[44100];
        mixer.Mix(samples);
        Check(opl.Rendered == 44100 && samples.Any(value => value != 0) && output.ToString().Contains("write:", StringComparison.Ordinal),
            "Managed LDS updates did not drive OPL registers and mixer rendering.");
        using var archiveBytes = new MemoryStream();
        BinaryFile.WriteUInt16(archiveBytes, 2);
        BinaryFile.WriteUInt32(archiveBytes, 10); BinaryFile.WriteUInt32(archiveBytes, 10);
        archiveBytes.Write(fixture); archiveBytes.Position = 0;
        var controller = new MusicController(MusicArchive.Load(archiveBytes), 44100, player);
        controller.PlaySong(1); controller.Mixer.Mix(new short[2048]);
        Check(controller.SongPlaying == 1 && !controller.Mixer.MusicStopped && opl.Rendered == 46148,
            "Music archive/control/mixer integration did not reach the LDS sequencer.");
        controller.Stop(); controller.Mixer.Mix(new short[16]);
        Check(opl.Rendered == 46148, "Stopped music still invoked OPL synthesis.");
        var invalidArpeggio = fixture.ToArray();
        invalidArpeggio[17 + 20] = 13;
        invalidArpeggio.AsSpan(17 + 21, 12).Fill(1);
        for (var i = 0; i < 36; i++)
        {
            var start = 3073 + i * 64;
            invalidArpeggio[start] = 255; invalidArpeggio[start + 1] = 128;
        }
        player.Load(invalidArpeggio);
        for (var i = 0; i < 3; i++) player.Update();
        player.PlaySound(0, 0, 512);
        Throws<InvalidDataException>(() =>
        {
            for (var i = 0; i < 13; i++) player.Update();
        });
    }

    private static void CompareNative(string executable, string path, int scenario, string expected)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(path); start.ArgumentList.Add(scenario.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start LDS oracle.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("LDS oracle timed out.");
        }
        var actual = stdout.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
        var error = stderr.GetAwaiter().GetResult();
        if (process.ExitCode == 0 && actual == expected && error.Length == 0) return;
        var actualLines = actual.Split('\n'); var expectedLines = expected.Split('\n');
        var first = Enumerable.Range(0, Math.Min(actualLines.Length, expectedLines.Length))
            .FirstOrDefault(i => actualLines[i] != expectedLines[i], -1);
        throw new InvalidOperationException($"Native LDS parity differs in scenario {scenario} at {first}: {(first < 0 ? "" : actualLines[first] + " / " + expectedLines[first])}; {error}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class Opl(StringBuilder output) : IOplSynthesizer
    {
        private uint _signature;
        public int Rendered { get; private set; }
        public void Reset() { _signature = 1; output.Append("reset\n"); }
        public void Write(byte register, byte value)
        {
            _signature = unchecked(_signature * 31 + register * 256u + value);
            output.Append($"write:{register},{value}\n");
        }
        public void Render(Span<short> samples)
        {
            for (var i = 0; i < samples.Length; i++) samples[i] = unchecked((short)(_signature + (uint)Rendered++));
        }
    }
}
