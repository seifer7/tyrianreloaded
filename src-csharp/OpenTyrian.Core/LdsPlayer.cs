/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;

namespace OpenTyrian.Core;

public interface IOplSynthesizer
{
    void Reset();
    void Write(byte register, byte value);
    void Render(Span<short> samples);
    void Render(Span<short> samples, Func<uint>? nextRandom) => Render(samples);
}

public static class OplSample
{
    public static short ClipInt16(int sample) =>
        (short)Math.Clamp(sample, short.MinValue, short.MaxValue);
}

public static class OplWaveforms
{
    private const int WavePrecision = 1024;
    private static readonly uint[] Starts = [1024, 512, 1024, 768, 0, 0, 1280, 2048];
    private static readonly uint[] Masks = [1023, 1023, 511, 511, 1023, 767, 511, 1023];
    private static readonly uint[] InitialPositions = [0, 512, 0, 256, 0, 0, 0, 128];
    private static readonly short[] Table = CreateTable();

    public static short[] GetTable() => Table;

    public static uint GetStart(byte selection) => Starts[selection & 7];

    public static uint GetMask(byte selection) => Masks[selection & 7];

    public static uint GetInitialPosition(byte selection) => InitialPositions[selection & 7];

    private static short[] CreateTable()
    {
        var table = new short[WavePrecision * 3];
        for (var i = 0; i < (WavePrecision >> 1); i++)
        {
            table[(i << 1) + WavePrecision] = (short)(16384 * Math.Sin((i << 1) * Math.PI * 2 / WavePrecision));
            table[(i << 1) + 1 + WavePrecision] = (short)(16384 * Math.Sin(((i << 1) + 1) * Math.PI * 2 / WavePrecision));
            table[i] = table[(i << 1) + WavePrecision];
        }
        for (var i = 0; i < (WavePrecision >> 3); i++)
        {
            table[i + (WavePrecision << 1)] = (short)(table[i + (WavePrecision >> 3)] - 16384);
            table[i + ((WavePrecision * 17) >> 3)] = (short)(table[i + (WavePrecision >> 2)] + 16384);
        }
        return table;
    }
}

public sealed class OplChipState : IOplSynthesizer
{
    private const double ChipFrequency = 14318180.0 / 288.0;
    private const uint FixedPoint = 0x10000;
    private const uint FixedPointLfo = 0x1000000;
    private const int BlockBufferSize = 512;
    private const int TremoloTableSize = 53;
    private static readonly byte[] ModulatorBases = [0, 1, 2, 8, 9, 10, 16, 17, 18];
    private static readonly byte[] ModulatorOperators =
        [0, 1, 2, 0, 1, 2, 0, 0, 3, 4, 5, 3, 4, 5, 0, 0, 6, 7, 8, 6, 7, 8,
         18, 19, 20, 18, 19, 20, 0, 0, 21, 22, 23, 21, 22, 23, 0, 0, 24, 25, 26, 24, 25, 26];
    private static readonly byte[] RegisterOperators =
        [0, 1, 2, 9, 10, 11, 0, 0, 3, 4, 5, 12, 13, 14, 0, 0, 6, 7, 8, 15, 16, 17,
         18, 19, 20, 27, 28, 29, 0, 0, 21, 22, 23, 30, 31, 32, 0, 0, 24, 25, 26, 33, 34, 35];

    private OplChipState(uint sampleRate, bool opl3)
    {
        SampleRate = sampleRate;
        IsOpl3 = opl3;
        GeneratorIncrement = (uint)(ChipFrequency * FixedPoint / sampleRate);
        FrequencyTables = new OplFrequencyTables(sampleRate);
        Registers = new byte[opl3 ? 512 : 256];
        WaveSelections = new byte[opl3 ? 44 : 22];
        Operators = Enumerable.Range(0, opl3 ? 36 : 18)
            .Select(_ => new OplOperatorTimingState())
            .ToArray();
        foreach (var op in Operators)
        {
            op.Initialize();
            if (opl3)
            {
                op.LeftPan = 1;
                op.RightPan = 1;
            }
        }
        Envelopes = Enumerable.Range(0, opl3 ? 36 : 18)
            .Select(_ => new OplEnvelopeState())
            .ToArray();
        foreach (var envelope in Envelopes)
            envelope.Initialize();

        VibratoTable = [8, 4, 0, -4, -8, -4, 0, 4];
        VibratoIncrement = (uint)(16384 * ChipFrequency / sampleRate);
        VibratoPosition = 0;
        VibratoConstant = new int[BlockBufferSize];
        TremoloTable = CreateTremoloTable();
        TremoloIncrement = (uint)(TremoloTableSize * 3.7 * FixedPointLfo / sampleRate);
        TremoloPosition = 0;
        TremoloConstant = Enumerable.Repeat((int)FixedPoint, BlockBufferSize).ToArray();
    }

    public uint SampleRate { get; }
    public bool IsOpl3 { get; }
    public uint GeneratorIncrement { get; }
    public OplFrequencyTables FrequencyTables { get; }
    public byte[] Registers { get; }
    public byte[] WaveSelections { get; }
    public OplOperatorTimingState[] Operators { get; }
    public OplEnvelopeState[] Envelopes { get; }
    public int[] VibratoTable { get; }
    public uint VibratoIncrement { get; }
    public uint VibratoPosition { get; private set; }
    public int[] VibratoConstant { get; }
    public int[] TremoloTable { get; }
    public uint TremoloIncrement { get; }
    public uint TremoloPosition { get; private set; }
    public int[] TremoloConstant { get; }
    public byte Status { get; private set; }
    public uint RegisterIndex { get; private set; }

    public void Reset()
    {
        Array.Clear(Registers);
        Array.Clear(WaveSelections);
        foreach (var op in Operators)
        {
            op.Initialize();
            if (IsOpl3)
            {
                op.LeftPan = 1;
                op.RightPan = 1;
            }
        }
        foreach (var envelope in Envelopes)
            envelope.Initialize();
        Array.Clear(VibratoConstant);
        Array.Fill(TremoloConstant, (int)FixedPoint);
        VibratoPosition = 0;
        TremoloPosition = 0;
        Status = 0;
        RegisterIndex = 0;
    }

    public void Write(byte register, byte value) => WriteRegister(register, value);

    public void Render(Span<short> samples) => Render(samples, null);

    public void Render(Span<short> samples, Func<uint>? nextRandom)
    {
        if (IsOpl3 && samples.Length % 2 != 0)
            throw new ArgumentException("OPL3 output requires interleaved stereo samples.", nameof(samples));

        var channels = IsOpl3 ? samples.Length / 2 : samples.Length;
        Span<int> left = stackalloc int[BlockBufferSize];
        Span<int> right = stackalloc int[BlockBufferSize];
        Span<int> vibrato = stackalloc int[BlockBufferSize];
        Span<int> tremolo = stackalloc int[BlockBufferSize];
        for (var blockStart = 0; blockStart < channels; blockStart += BlockBufferSize)
        {
            var blockLength = Math.Min(BlockBufferSize, channels - blockStart);
            left[..blockLength].Clear();
            right[..blockLength].Clear();

            BuildModulationTables(vibrato[..blockLength], tremolo[..blockLength]);
            RenderPercussion(left[..blockLength], right[..blockLength],
                vibrato[..blockLength], tremolo[..blockLength], nextRandom);
            for (var channel = IsOpl3 ? 17 : 8; channel >= 0; channel--)
            {
                if ((Registers[0xbd] & 0x20) != 0 && channel is >= 6 and <= 8)
                    continue;

                var operatorBase = channel < 9 ? channel : channel + 9;
                var registerBase = channel < 9 ? channel : channel + 247;
                if (IsOpl3 && (Registers[0x105] & 1) != 0 &&
                    Operators[operatorBase].IsFourOperatorAttached)
                    continue;

                RenderTwoOperatorChannel(
                    operatorBase, registerBase, left[..blockLength], right[..blockLength],
                    vibrato[..blockLength], tremolo[..blockLength]);
            }

            for (var sample = 0; sample < blockLength; sample++)
            {
                if (IsOpl3)
                {
                    samples[(blockStart + sample) * 2] = OplSample.ClipInt16(left[sample]);
                    samples[(blockStart + sample) * 2 + 1] = OplSample.ClipInt16(
                        (Registers[0x105] & 1) != 0 ? right[sample] : left[sample]);
                }
                else
                {
                    samples[blockStart + sample] = OplSample.ClipInt16(left[sample]);
                }
            }
        }
    }

    private void RenderPercussion(
        Span<int> left, Span<int> right,
        ReadOnlySpan<int> vibrato, ReadOnlySpan<int> tremolo, Func<uint>? nextRandom)
    {
        if ((Registers[0xbd] & 0x20) == 0)
            return;

        var bassModulator = Operators[6];
        var bassCarrier = Operators[15];
        if ((Registers[0xc6] & 1) != 0)
        {
            if (bassCarrier.Mode != OplOperatorMode.Off)
            {
                for (var i = 0; i < left.Length; i++)
                {
                    AdvanceAndRenderOperator(bassCarrier, 15, 0);
                    bassCarrier.Render(0, TremoloFor(bassCarrier, tremolo[i]));
                    AddPercussionOutput(left, right, i, unchecked(bassCarrier.Output * 2));
                }
            }
        }
        else if (bassModulator.Mode != OplOperatorMode.Off || bassCarrier.Mode != OplOperatorMode.Off)
        {
            for (var i = 0; i < left.Length; i++)
            {
                AdvanceAndRenderOperator(bassModulator, 6, 0);
                bassModulator.Render(
                    unchecked((bassModulator.LastOutput + bassModulator.Output) * bassModulator.FeedbackAmount / 2),
                    TremoloFor(bassModulator, tremolo[i]));
                AdvanceAndRenderOperator(bassCarrier, 15, 0);
                bassCarrier.Render(unchecked(bassModulator.Output * (int)FixedPoint),
                    TremoloFor(bassCarrier, tremolo[i]));
                AddPercussionOutput(left, right, i, unchecked(bassCarrier.Output * 2));
            }
        }

        var tom = Operators[8];
        if (tom.Mode != OplOperatorMode.Off)
        {
            for (var i = 0; i < left.Length; i++)
            {
                AdvanceAndRenderOperator(tom, 8, 0);
                tom.Render(0, TremoloFor(tom, tremolo[i]));
                AddPercussionOutput(left, right, i, unchecked(tom.Output * 2));
            }
        }

        var hihat = Operators[7];
        var snare = Operators[16];
        var cymbal = Operators[17];
        if (hihat.Mode == OplOperatorMode.Off &&
            snare.Mode == OplOperatorMode.Off && cymbal.Mode == OplOperatorMode.Off)
            return;
        if (nextRandom is null)
            throw new InvalidOperationException("OPL rhythm-mode rendering requires a native-compatible random source.");

        for (var i = 0; i < left.Length; i++)
        {
            OplOperatorTimingState.AdvanceDrums(
                hihat, 0, snare, 0, cymbal, 0, GeneratorIncrement, nextRandom);
            AdvanceEnvelopeAfterDrumAdvance(hihat, 7);
            AdvanceEnvelopeAfterDrumAdvance(snare, 16);
            AdvanceEnvelopeAfterDrumAdvance(cymbal, 17);
            hihat.Render(0, TremoloFor(hihat, tremolo[i]));
            snare.Render(0, TremoloFor(snare, tremolo[i]));
            cymbal.Render(0, TremoloFor(cymbal, tremolo[i]));
            AddPercussionOutput(left, right, i,
                unchecked(unchecked(hihat.Output + snare.Output + cymbal.Output) * 2));
        }
    }

    private static int TremoloFor(OplOperatorTimingState op, int tremolo) =>
        op.Tremolo ? tremolo : (int)FixedPoint;

    private void AdvanceEnvelopeAfterDrumAdvance(OplOperatorTimingState op, int index)
    {
        var envelope = Envelopes[index];
        envelope.GeneratorPosition = op.GeneratorPosition;
        envelope.Mode = op.Mode;
        switch (op.Mode)
        {
            case OplOperatorMode.Attack:
                envelope.AdvanceAttack();
                break;
            case OplOperatorMode.Decay:
                envelope.AdvanceDecay();
                break;
            case OplOperatorMode.Release:
            case OplOperatorMode.SustainNoKeep:
                envelope.AdvanceRelease();
                break;
            case OplOperatorMode.Sustain:
                envelope.AdvanceSustain();
                break;
            case OplOperatorMode.Off:
                envelope.AdvanceOff();
                break;
            default:
                throw new InvalidOperationException($"Unknown OPL envelope mode {op.Mode}.");
        }
        op.GeneratorPosition = envelope.GeneratorPosition;
        op.Mode = envelope.Mode;
        op.StepAmplitude = envelope.StepAmplitude;
    }

    private void AddPercussionOutput(Span<int> left, Span<int> right, int index, int value)
    {
        if (IsOpl3 && (Registers[0x105] & 1) != 0)
        {
            left[index] = unchecked(left[index] + value);
            right[index] = unchecked(right[index] + value);
        }
        else
        {
            left[index] = unchecked(left[index] + value);
        }
    }

    private void BuildModulationTables(Span<int> vibrato, Span<int> tremolo)
    {
        var vibrationShift = (Registers[0xbd] & 0x40) == 0 ? 1 : 0;
        for (var i = 0; i < vibrato.Length; i++)
        {
            VibratoPosition = unchecked(VibratoPosition + VibratoIncrement);
            if (VibratoPosition / FixedPointLfo >= VibratoTable.Length)
                VibratoPosition = unchecked(VibratoPosition - (uint)VibratoTable.Length * FixedPointLfo);
            vibrato[i] = VibratoTable[VibratoPosition / FixedPointLfo] >> vibrationShift;

            TremoloPosition = unchecked(TremoloPosition + TremoloIncrement);
            if (TremoloPosition / FixedPointLfo >= TremoloTableSize)
                TremoloPosition = unchecked(TremoloPosition - TremoloTableSize * FixedPointLfo);
            var tableOffset = (Registers[0xbd] & 0x80) != 0 ? 0 : TremoloTableSize;
            tremolo[i] = TremoloTable[tableOffset + TremoloPosition / FixedPointLfo];
        }
    }

    private void RenderTwoOperatorChannel(
        int operatorBase, int registerBase,
        Span<int> left, Span<int> right, ReadOnlySpan<int> vibrato, ReadOnlySpan<int> tremolo)
    {
        var modulator = Operators[operatorBase];
        var carrier = Operators[operatorBase + 9];
        if (modulator.Mode == OplOperatorMode.Off && carrier.Mode == OplOperatorMode.Off)
            return;

        var additive = (Registers[0xc0 + registerBase] & 1) != 0;
        for (var i = 0; i < left.Length; i++)
        {
            AdvanceAndRenderOperator(modulator, operatorBase, 0);
            var modulatorTremolo = modulator.Tremolo ? tremolo[i] : (int)FixedPoint;
            modulator.Render(unchecked((modulator.LastOutput + modulator.Output) * modulator.FeedbackAmount / 2), modulatorTremolo);

            AdvanceAndRenderOperator(carrier, operatorBase + 9, 0);
            var carrierTremolo = carrier.Tremolo ? tremolo[i] : (int)FixedPoint;
            carrier.Render(additive ? 0 : unchecked(modulator.Output * (int)FixedPoint), carrierTremolo);

            var channelValue = additive
                ? unchecked(carrier.Output + modulator.Output)
                : carrier.Output;
            if (IsOpl3 && (Registers[0x105] & 1) != 0)
            {
                left[i] = unchecked(left[i] + channelValue * modulator.LeftPan);
                right[i] = unchecked(right[i] + channelValue * modulator.RightPan);
            }
            else
            {
                left[i] = unchecked(left[i] + channelValue);
            }
        }
    }

    private void AdvanceAndRenderOperator(OplOperatorTimingState op, int index, int vibrato)
    {
        op.Advance(vibrato, GeneratorIncrement);
        var envelope = Envelopes[index];
        envelope.GeneratorPosition = op.GeneratorPosition;
        envelope.Mode = op.Mode;
        switch (op.Mode)
        {
            case OplOperatorMode.Attack:
                envelope.AdvanceAttack();
                break;
            case OplOperatorMode.Decay:
                envelope.AdvanceDecay();
                break;
            case OplOperatorMode.Release:
            case OplOperatorMode.SustainNoKeep:
                envelope.AdvanceRelease();
                break;
            case OplOperatorMode.Sustain:
                envelope.AdvanceSustain();
                break;
            case OplOperatorMode.Off:
                envelope.AdvanceOff();
                break;
            default:
                throw new InvalidOperationException($"Unknown OPL envelope mode {op.Mode}.");
        }
        op.GeneratorPosition = envelope.GeneratorPosition;
        op.Mode = envelope.Mode;
        op.StepAmplitude = envelope.StepAmplitude;
    }

    public byte ReadPort(uint port)
    {
        if ((port & 1) != 0)
            return IsOpl3 ? (byte)0 : byte.MaxValue;
        return IsOpl3 ? Status : (byte)(Status | 6);
    }

    public void WriteIndex(uint port, byte value)
    {
        uint index = value;
        if (IsOpl3 && (port & 3) != 0 &&
            ((Registers[0x105] & 1) != 0 || index == 5))
            index |= 0x100;
        RegisterIndex = index;
    }

    public void WriteRegister(uint register, byte value)
    {
        if (register >= Registers.Length)
            throw new ArgumentOutOfRangeException(nameof(register));

        Registers[register] = value;
        var secondSet = register & 0x100;
        switch (register & 0xf0)
        {
            case 0x00:
                if (register == 0x104 && IsOpl3)
                {
                    ConfigureFourOperatorChannels(value);
                }
                else if (register == 0x04)
                    Status = (byte)((value & 0x80) != 0 ? Status & ~0x60 : 0);
                break;
            case 0x20:
            case 0x30:
                ConfigureOperatorMisc(register, secondSet);
                break;
            case 0x40:
            case 0x50:
                ConfigureOperatorLevel(register, secondSet);
                break;
            case 0x60:
            case 0x70:
                ConfigureOperatorAttackDecay(register, secondSet);
                break;
            case 0x80:
            case 0x90:
                ConfigureOperatorSustainRelease(register, secondSet);
                break;
            case 0xa0:
                ConfigureChannelFrequencies(register);
                break;
            case 0xb0:
                if (register == 0xbd)
                {
                    if (!IsOpl3 || secondSet == 0)
                        ConfigurePercussion(value);
                }
                else
                {
                    ConfigureChannelKey(register, value);
                }
                break;
            case 0xc0:
                ConfigureChannelFeedback(register, value);
                break;
            case 0xe0:
            case 0xf0:
                ConfigureOperatorWaveform(register, secondSet);
                break;
        }
    }

    public static OplChipState Initialize(uint sampleRate, bool opl3 = false)
    {
        if (sampleRate == 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        return new OplChipState(sampleRate, opl3);
    }

    private static int[] CreateTremoloTable()
    {
        var table = new int[TremoloTableSize * 2];
        for (var i = 0; i < TremoloTableSize; i++)
        {
            var integer = i < 14 ? i - 13 : i < 41 ? -i + 14 : i - 40 - 26;
            var fine = integer * 4.8 / 26.0 / 6.0;
            var coarse = (integer / 4) * 1.2 / 6.0 / 6.0;
            table[i] = (int)(Math.Pow(2, fine) * FixedPoint);
            table[TremoloTableSize + i] = (int)(Math.Pow(2, coarse) * FixedPoint);
        }
        return table;
    }

    private void ConfigureFourOperatorChannels(byte value)
    {
        ReadOnlySpan<int> bases = [0, 1, 2, 18, 19, 20];
        ReadOnlySpan<int> attached = [3, 4, 5, 21, 22, 23];
        for (var i = 0; i < bases.Length; i++)
        {
            var enabled = (value & (1 << i)) != 0;
            Operators[bases[i]].IsFourOperator = enabled;
            Operators[attached[i]].IsFourOperatorAttached = enabled;
        }
    }

    private void ConfigureOperatorMisc(uint register, uint secondSet)
    {
        var number = register & 7;
        var baseRegister = (register - 0x20) & 0xff;
        if (number >= 6 || baseRegister >= 22)
            return;

        var mappedBase = (int)(baseRegister + (secondSet != 0 ? 22 : 0));
        var modulator = ModulatorOperators[mappedBase];
        var operatorIndex = modulator + (number < 3 ? 0 : 9);
        var operatorRegister = baseRegister + secondSet;
        var channelBase = secondSet != 0 ? (uint)(modulator - 18) + secondSet : modulator;

        Envelopes[operatorIndex].ConfigureSustainKeep(Registers[0x20 + operatorRegister]);
        Operators[operatorIndex].ConfigureVibrato(Registers[0x20 + operatorRegister]);
        ConfigureMappedFrequency(modulator, channelBase, operatorRegister, operatorIndex);
    }

    private void ConfigureOperatorLevel(uint register, uint secondSet)
    {
        var number = register & 7;
        var baseRegister = (register - 0x40) & 0xff;
        if (number >= 6 || baseRegister >= 22)
            return;

        var mappedBase = (int)(baseRegister + (secondSet != 0 ? 22 : 0));
        var modulator = ModulatorOperators[mappedBase];
        var operatorIndex = modulator + (number < 3 ? 0 : 9);
        var channelBase = secondSet != 0 ? (uint)(modulator - 18) + secondSet : modulator;
        ConfigureMappedFrequency(modulator, channelBase, baseRegister + secondSet, operatorIndex);
    }

    private void ConfigureOperatorAttackDecay(uint register, uint secondSet)
    {
        var number = register & 7;
        var baseRegister = (register - 0x60) & 0xff;
        if (number >= 6 || baseRegister >= 22)
            return;

        var mappedBase = (int)(baseRegister + (secondSet != 0 ? 22 : 0));
        var operatorIndex = RegisterOperators[mappedBase];
        var registerBase = baseRegister + secondSet;
        Envelopes[operatorIndex].ConfigureAttack(
            Registers[0x60 + registerBase], Operators[operatorIndex].TimeOffset,
            FrequencyTables.ReciprocalSampleRate, IsOpl3);
        Envelopes[operatorIndex].ConfigureDecayRate(
            Registers[0x60 + registerBase], Operators[operatorIndex].TimeOffset,
            FrequencyTables.ReciprocalSampleRate);
    }

    private void ConfigureOperatorSustainRelease(uint register, uint secondSet)
    {
        var number = register & 7;
        var baseRegister = (register - 0x80) & 0xff;
        if (number >= 6 || baseRegister >= 22)
            return;

        var mappedBase = (int)(baseRegister + (secondSet != 0 ? 22 : 0));
        var operatorIndex = RegisterOperators[mappedBase];
        var registerBase = baseRegister + secondSet;
        Envelopes[operatorIndex].ConfigureReleaseRate(
            Registers[0x80 + registerBase], Operators[operatorIndex].TimeOffset,
            FrequencyTables.ReciprocalSampleRate);
        Envelopes[operatorIndex].ConfigureSustainLevel(Registers[0x80 + registerBase]);
    }

    private void ConfigureChannelFrequencies(uint register)
    {
        var channel = (register - 0xa0) & 0xff;
        if (channel >= 9)
            return;

        var secondSet = register & 0x100;
        var operatorBase = (int)(channel + (secondSet != 0 ? 18 : 0));
        if (IsOpl3 && (Registers[0x105] & 1) != 0 && Operators[operatorBase].IsFourOperatorAttached)
            return;

        var channelBase = channel + secondSet;
        var modulatorBase = ModulatorBases[channel] + secondSet;
        ConfigureFrequency(channelBase, modulatorBase, operatorBase);
        ConfigureFrequency(channelBase, modulatorBase + 3, operatorBase + 9);
        if (IsFourOperatorEnabled(operatorBase))
        {
            ConfigureFrequency(channelBase, modulatorBase + 8, operatorBase + 3);
            ConfigureFrequency(channelBase, modulatorBase + 11, operatorBase + 12);
        }
    }

    private void ConfigureChannelKey(uint register, byte value)
    {
        var channel = (register - 0xb0) & 0xff;
        if (channel >= 9)
            return;

        var secondSet = register & 0x100;
        var operatorBase = (int)(channel + (secondSet != 0 ? 18 : 0));
        if (IsOpl3 && (Registers[0x105] & 1) != 0 && Operators[operatorBase].IsFourOperatorAttached)
            return;

        var modulatorBase = ModulatorBases[channel] + secondSet;
        if ((value & 0x20) != 0)
        {
            EnableOperator(modulatorBase, operatorBase, 1);
            EnableOperator(modulatorBase + 3, operatorBase + 9, 1);
            if (IsFourOperatorEnabled(operatorBase))
            {
                EnableOperator(modulatorBase + 8, operatorBase + 3, 1);
                EnableOperator(modulatorBase + 11, operatorBase + 12, 1);
            }
        }
        else
        {
            DisableOperator(operatorBase, 1);
            DisableOperator(operatorBase + 9, 1);
            if (IsFourOperatorEnabled(operatorBase))
            {
                DisableOperator(operatorBase + 3, 1);
                DisableOperator(operatorBase + 12, 1);
            }
        }

        var channelBase = channel + secondSet;
        ConfigureFrequency(channelBase, modulatorBase, operatorBase);
        ConfigureFrequency(channelBase, modulatorBase + 3, operatorBase + 9);
        if (IsFourOperatorEnabled(operatorBase))
        {
            ConfigureFrequency(channelBase, modulatorBase + 8, operatorBase + 3);
            ConfigureFrequency(channelBase, modulatorBase + 11, operatorBase + 12);
        }
    }

    private void ConfigurePercussion(byte value)
    {
        ConfigurePercussionOperator((value & 0x30) == 0x30, 6, 16, 6);
        ConfigurePercussionOperator((value & 0x30) == 0x30, 15, 19, 6);
        ConfigurePercussionOperator((value & 0x28) == 0x28, 16, 20, 7);
        ConfigurePercussionOperator((value & 0x24) == 0x24, 8, 18, 8);
        ConfigurePercussionOperator((value & 0x22) == 0x22, 17, 21, 8);
        ConfigurePercussionOperator((value & 0x21) == 0x21, 7, 17, 7);
    }

    private void ConfigurePercussionOperator(bool enabled, int operatorIndex, uint registerBase, uint channel)
    {
        if (enabled)
            EnableOperator(registerBase, operatorIndex, 2);
        else
            DisableOperator(operatorIndex, 2);
        if (enabled)
            ConfigureFrequency(channel, registerBase, operatorIndex);
    }

    private void ConfigureChannelFeedback(uint register, byte value)
    {
        var channel = (register - 0xc0) & 0xff;
        if (channel >= 9)
            return;

        var secondSet = register & 0x100;
        var operatorBase = (int)(channel + (secondSet != 0 ? 18 : 0));
        Operators[operatorBase].ConfigureFeedback(value);
        if (IsOpl3)
        {
            Operators[operatorBase].LeftPan = (value >> 4) & 1;
            Operators[operatorBase].RightPan = (value >> 5) & 1;
        }
    }

    private void ConfigureOperatorWaveform(uint register, uint secondSet)
    {
        var number = register & 7;
        var baseRegister = (register - 0xe0) & 0xff;
        if (number >= 6 || baseRegister >= 22)
            return;

        if (IsOpl3)
        {
            var mappedBase = (int)(baseRegister + (secondSet != 0 ? 22 : 0));
            WaveSelections[mappedBase] = (byte)((Registers[0x105] & 1) != 0
                ? Registers[register] & 7
                : Registers[register] & 3);
            var modulator = ModulatorOperators[mappedBase];
            Operators[modulator + (number < 3 ? 0 : 9)].ConfigureWaveform(WaveSelections[mappedBase]);
        }
        else if ((Registers[0x01] & 0x20) != 0)
        {
            WaveSelections[baseRegister] = (byte)(Registers[register] & 3);
            var modulator = ModulatorOperators[baseRegister];
            Operators[modulator + (number < 3 ? 0 : 9)].ConfigureWaveform(WaveSelections[baseRegister]);
        }
    }

    private bool IsFourOperatorEnabled(int operatorBase) =>
        IsOpl3 && (Registers[0x105] & 1) != 0 && Operators[operatorBase].IsFourOperator;

    private void EnableOperator(uint registerBase, int operatorIndex, uint activity)
    {
        Operators[operatorIndex].Enable(WaveSelections[WaveSelectionIndex(registerBase)], activity);
        Envelopes[operatorIndex].Mode = Operators[operatorIndex].Mode;
    }

    private void DisableOperator(int operatorIndex, uint activity)
    {
        Operators[operatorIndex].Disable(activity);
        Envelopes[operatorIndex].Mode = Operators[operatorIndex].Mode;
    }

    private int WaveSelectionIndex(uint registerBase) =>
        (int)(registerBase >= 0x100 ? registerBase - 0x100 + 22 : registerBase);

    private void ConfigureMappedFrequency(int modulator, uint channelBase, uint registerBase, int operatorIndex)
    {
        if (IsOpl3 && (Registers[0x105] & 1) != 0 && Operators[modulator].IsFourOperatorAttached)
            channelBase -= 3;
        ConfigureFrequency(channelBase, registerBase, operatorIndex);
    }

    private void ConfigureFrequency(uint channelBase, uint registerBase, int operatorIndex)
    {
        var operatorRegister = (int)registerBase;
        var frequencyRegisters = new OplFrequencyRegisters(
            Registers[0xa0 + (int)channelBase],
            Registers[0xb0 + (int)channelBase],
            Registers[0x08],
            Registers[0x20 + operatorRegister],
            Registers[0x40 + operatorRegister],
            Registers[0x60 + operatorRegister],
            Registers[0x80 + operatorRegister]);
        Operators[operatorIndex].ConfigureFrequency(
            frequencyRegisters, FrequencyTables, Envelopes[operatorIndex], IsOpl3);
        Envelopes[operatorIndex].ConfigureSustainKeep(frequencyRegisters.TremoloVibratoKsrMultiplier);
        Operators[operatorIndex].ConfigureVibrato(frequencyRegisters.TremoloVibratoKsrMultiplier);
    }
}

public sealed class OplFrequencyTables
{
    private const double ChipFrequency = 14318180.0 / 288.0;
    private static readonly double[] BaseFrequencyMultipliers =
        [0.5, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 12, 12, 15, 15];
    private static readonly double[] KeyScaleMultipliers = [0, 0.5, 0.25, 1];
    private static readonly byte[,] KeyScaleLevels = CreateKeyScaleLevels();
    private readonly double[] _frequencyMultipliers;

    public OplFrequencyTables(double sampleRate)
    {
        if (!double.IsFinite(sampleRate) || sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));

        ReciprocalSampleRate = 1.0 / sampleRate;
        _frequencyMultipliers = BaseFrequencyMultipliers
            .Select(multiplier => multiplier * ChipFrequency / 1024 * 0x10000 * ReciprocalSampleRate)
            .ToArray();
    }

    public double ReciprocalSampleRate { get; }

    public double FrequencyMultiplier(uint index) => _frequencyMultipliers[index];

    public double KeyScaleMultiplier(uint index) => KeyScaleMultipliers[index];

    public byte KeyScaleLevel(uint octave, uint fNumberIndex) => KeyScaleLevels[octave, fNumberIndex];

    private static byte[,] CreateKeyScaleLevels()
    {
        byte[,] levels = new byte[8, 16];
        byte[] highestOctave = [0, 24, 32, 37, 40, 43, 45, 47, 48, 50, 51, 52, 53, 54, 55, 56];
        for (var note = 0; note < highestOctave.Length; note++)
            levels[7, note] = highestOctave[note];
        for (var octave = 6; octave >= 0; octave--)
        {
            for (var note = 0; note < 16; note++)
                levels[octave, note] = (byte)Math.Max(levels[octave + 1, note] - 8, 0);
        }
        return levels;
    }
}

public readonly record struct OplFrequencyRegisters(
    byte FrequencyLow,
    byte KeyOnBlock,
    byte NoteSelection,
    byte TremoloVibratoKsrMultiplier,
    byte KeyScaleOutputLevel,
    byte AttackDecay,
    byte SustainRelease);

public sealed class OplOperatorTimingState
{
    public uint TimeCount { get; set; }
    public uint WaveformPosition { get; set; }
    public uint WaveformStart { get; set; }
    public uint TimeIncrement { get; set; }
    public uint GeneratorPosition { get; set; }
    public short[] Waveform { get; set; } = [];
    public uint WaveformMask { get; set; }
    public double StepAmplitude { get; set; }
    public double Volume { get; set; }
    public OplOperatorMode Mode { get; set; }
    public int Output { get; set; }
    public int LastOutput { get; set; }
    public int FeedbackAmount { get; set; }
    public uint ActivityState { get; private set; }
    public int FrequencyHigh { get; private set; }
    public uint TimeOffset { get; private set; }
    public bool IsFourOperator { get; set; }
    public bool IsFourOperatorAttached { get; set; }
    public int LeftPan { get; set; }
    public int RightPan { get; set; }

    public void Initialize()
    {
        TimeCount = 0;
        WaveformPosition = 0;
        WaveformStart = OplWaveforms.GetStart(0);
        TimeIncrement = 0;
        GeneratorPosition = 0;
        Waveform = OplWaveforms.GetTable();
        WaveformMask = OplWaveforms.GetMask(0);
        StepAmplitude = 0;
        Volume = 0;
        Mode = OplOperatorMode.Off;
        Output = 0;
        LastOutput = 0;
        FeedbackAmount = 0;
        ActivityState = 0;
        FrequencyHigh = 0;
        TimeOffset = 0;
        IsFourOperator = false;
        IsFourOperatorAttached = false;
        LeftPan = 0;
        RightPan = 0;
        Vibrato = false;
        Tremolo = false;
    }

    public bool Vibrato { get; set; }
    public bool Tremolo { get; set; }

    public void Advance(int vibrato, uint generatorIncrement)
    {
        WaveformPosition = TimeCount;
        AdvanceTime(vibrato, generatorIncrement);
    }

    public void Render(int modulator, int tremolo)
    {
        if (Mode == OplOperatorMode.Off)
            return;

        LastOutput = Output;
        var waveformIndex = unchecked((uint)(WaveformPosition + (uint)modulator)) / 0x10000;
        var sample = Waveform[unchecked((int)(WaveformStart + (waveformIndex & WaveformMask)))];
        Output = (int)(StepAmplitude * Volume * sample * tremolo / 16.0);
    }

    public void ConfigureWaveform(byte selection)
    {
        Waveform = OplWaveforms.GetTable();
        WaveformStart = OplWaveforms.GetStart(selection);
        WaveformMask = OplWaveforms.GetMask(selection);
    }

    public void Enable(byte waveformSelection, uint activityType)
    {
        if (ActivityState != 0)
            return;

        TimeCount = OplWaveforms.GetInitialPosition(waveformSelection) * 0x10000;
        Mode = OplOperatorMode.Attack;
        ActivityState |= activityType;
    }

    public void Disable(uint activityType)
    {
        if (ActivityState == 0)
            return;

        ActivityState &= ~activityType;
        if (ActivityState == 0 && Mode != OplOperatorMode.Off)
            Mode = OplOperatorMode.Release;
    }

    public void ConfigureFrequency(
        OplFrequencyRegisters registers,
        OplFrequencyTables tables,
        OplEnvelopeState envelope,
        bool opl3)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(envelope);

        var fNumber = ((uint)(registers.KeyOnBlock & 3) << 8) + registers.FrequencyLow;
        var octave = ((uint)registers.KeyOnBlock >> 2) & 7;
        FrequencyHigh = (int)((fNumber >> 7) & 7);

        var noteSelect = ((uint)registers.NoteSelection >> 6) & 1;
        TimeOffset = ((fNumber >> 9) & (noteSelect ^ 1)) | ((fNumber >> 8) & noteSelect);
        TimeOffset += octave << 1;
        if ((registers.TremoloVibratoKsrMultiplier & 0x10) == 0)
            TimeOffset >>= 2;

        TimeIncrement = (uint)((double)(fNumber << (int)octave) *
            tables.FrequencyMultiplier((uint)(registers.TremoloVibratoKsrMultiplier & 15)));
        var volumeInput = (registers.KeyScaleOutputLevel & 63) +
            tables.KeyScaleMultiplier((uint)(registers.KeyScaleOutputLevel >> 6)) *
            tables.KeyScaleLevel(octave, fNumber >> 6);
        Volume = Math.Pow(2, volumeInput * -0.125 - 14);

        envelope.ConfigureAttack(registers.AttackDecay, TimeOffset, tables.ReciprocalSampleRate, opl3);
        envelope.ConfigureDecayRate(registers.AttackDecay, TimeOffset, tables.ReciprocalSampleRate);
        envelope.ConfigureReleaseRate(registers.SustainRelease, TimeOffset, tables.ReciprocalSampleRate);
    }

    public void ConfigureVibrato(byte tremoloVibratoRegister)
    {
        Vibrato = (tremoloVibratoRegister & 0x40) != 0;
        Tremolo = (tremoloVibratoRegister & 0x80) != 0;
    }

    public void ConfigureFeedback(byte feedbackRegister)
    {
        var feedback = feedbackRegister & 14;
        FeedbackAmount = feedback == 0 ? 0 : (int)Math.Pow(2, (feedback >> 1) + 8);
    }

    private void AdvanceTime(int vibrato, uint generatorIncrement)
    {
        var vibratoIncrement = unchecked((int)TimeIncrement * vibrato) / 0x10000;
        TimeCount = unchecked(TimeCount + TimeIncrement + (uint)vibratoIncrement);
        GeneratorPosition = unchecked(GeneratorPosition + generatorIncrement);
    }

    public static void AdvanceDrums(
        OplOperatorTimingState hihat, int hihatVibrato,
        OplOperatorTimingState snare, int snareVibrato,
        OplOperatorTimingState cymbal, int cymbalVibrato,
        uint generatorIncrement, Func<uint> nextRandom)
    {
        ArgumentNullException.ThrowIfNull(nextRandom);
        var hihatCount = hihat.TimeCount / 0x10000;
        var cymbalCount = cymbal.TimeCount / 0x10000;
        var phaseBit = (((hihatCount & 0x88) ^ ((hihatCount << 5) & 0x80)) |
            ((cymbalCount ^ (cymbalCount << 2)) & 0x20)) != 0 ? 2u : 0u;
        var noiseBit = nextRandom() & 1;
        var snarePhaseBit = (hihatCount / 0x100) & 1;

        hihat.WaveformPosition = ((phaseBit << 8) | (0x34u << (int)(phaseBit ^ (noiseBit << 1)))) * 0x10000;
        hihat.AdvanceTime(hihatVibrato, generatorIncrement);

        snare.WaveformPosition = (((1 + snarePhaseBit) ^ noiseBit) << 8) * 0x10000;
        snare.AdvanceTime(snareVibrato, generatorIncrement);

        cymbal.WaveformPosition = ((1 + phaseBit) << 8) * 0x10000;
        cymbal.AdvanceTime(cymbalVibrato, generatorIncrement);
    }
}

public enum OplOperatorMode : uint
{
    Attack,
    Decay,
    Release,
    Sustain,
    SustainNoKeep,
    Off
}

public sealed class OplEnvelopeState
{
    private const uint FixedPoint = 0x10000;
    private const double ReleaseThreshold = 0.00000001;
    private static readonly double[] AttackConstants =
    [
        1.0 / 2.82624,
        1.0 / 2.25280,
        1.0 / 1.88416,
        1.0 / 1.59744
    ];
    private static readonly double[] DecayReleaseConstants =
    [
        1.0 / 39.28064,
        1.0 / 31.41608,
        1.0 / 26.17344,
        1.0 / 22.44608
    ];
    private static readonly byte[] AttackStepSkipMasks = [0xff, 0xfe, 0xee, 0xba, 0xaa];

    public uint GeneratorPosition { get; set; }
    public nint CurrentEnvelopeStep { get; set; }
    public nint ReleaseStepMask { get; set; }
    public nint DecayStepMask { get; set; }
    public nint AttackStepMask { get; set; }
    public nint AttackSkipMask { get; set; }
    public byte AttackSkipPosition { get; set; }
    public double Amplitude { get; set; }
    public double StepAmplitude { get; set; }
    public double ReleaseMultiplier { get; set; } = 1;
    public double DecayMultiplier { get; set; } = 1;
    public double SustainLevel { get; set; }
    public double Attack0 { get; set; }
    public double Attack1 { get; set; }
    public double Attack2 { get; set; }
    public double Attack3 { get; set; }
    public bool SustainKeep { get; set; }
    public OplOperatorMode Mode { get; set; }

    public void Initialize()
    {
        GeneratorPosition = 0;
        CurrentEnvelopeStep = 0;
        ReleaseStepMask = 0;
        DecayStepMask = 0;
        AttackStepMask = 0;
        AttackSkipMask = 0;
        AttackSkipPosition = 0;
        Amplitude = 0;
        StepAmplitude = 0;
        ReleaseMultiplier = 1;
        DecayMultiplier = 1;
        SustainLevel = 0;
        Attack0 = 0;
        Attack1 = 0;
        Attack2 = 0;
        Attack3 = 0;
        SustainKeep = false;
        Mode = OplOperatorMode.Off;
    }

    public void ConfigureAttack(byte attackDecayRegister, uint timeOffset, double reciprocalSampleRate, bool opl3)
    {
        var attackRate = (uint)(attackDecayRegister >> 4);
        if (attackRate == 0)
        {
            Attack0 = 0;
            Attack1 = 1;
            Attack2 = 0;
            Attack3 = 0;
            AttackStepMask = 0;
            AttackSkipMask = 0;
            return;
        }

        var frequency = Math.Pow(2, attackRate + (timeOffset >> 2) - 1) *
            AttackConstants[timeOffset & 3] * reciprocalSampleRate;
        Attack0 = 0.0377 * frequency;
        Attack1 = 10.73 * frequency + 1;
        Attack2 = -17.57 * frequency;
        Attack3 = 7.42 * frequency;

        var stepSkip = attackRate * 4 + timeOffset;
        var steps = stepSkip >> 2;
        AttackStepMask = (nint)((1u << (int)(steps <= 12 ? 12 - steps : 0)) - 1);
        var stepNumber = stepSkip <= 48 ? (int)(4 - (stepSkip & 3)) : 0;
        AttackSkipMask = AttackStepSkipMasks[stepNumber];

        if (stepSkip >= (opl3 ? 60u : 62u))
        {
            Attack0 = 2;
            Attack1 = 0;
            Attack2 = 0;
            Attack3 = 0;
        }
    }

    public void ConfigureSustainLevel(byte sustainReleaseRegister)
    {
        var sustainLevel = sustainReleaseRegister >> 4;
        SustainLevel = sustainLevel < 15 ? Math.Pow(2, sustainLevel * -0.5) : 0;
    }

    public void ConfigureDecayRate(byte attackDecayRegister, uint timeOffset, double reciprocalSampleRate)
    {
        var decayRate = (uint)(attackDecayRegister & 15);
        if (decayRate == 0)
        {
            DecayMultiplier = 1;
            DecayStepMask = 0;
            return;
        }

        var frequency = -7.4493 * DecayReleaseConstants[timeOffset & 3] * reciprocalSampleRate;
        DecayMultiplier = Math.Pow(2, frequency * Math.Pow(2, decayRate + (timeOffset >> 2)));
        DecayStepMask = CalculateEnvelopeStepMask(decayRate, timeOffset);
    }

    public void ConfigureReleaseRate(byte sustainReleaseRegister, uint timeOffset, double reciprocalSampleRate)
    {
        var releaseRate = (uint)(sustainReleaseRegister & 15);
        if (releaseRate == 0)
        {
            ReleaseMultiplier = 1;
            ReleaseStepMask = 0;
            return;
        }

        var frequency = -7.4493 * DecayReleaseConstants[timeOffset & 3] * reciprocalSampleRate;
        ReleaseMultiplier = Math.Pow(2, frequency * Math.Pow(2, releaseRate + (timeOffset >> 2)));
        ReleaseStepMask = CalculateEnvelopeStepMask(releaseRate, timeOffset);
    }

    public void ConfigureSustainKeep(byte tremoloVibratoRegister)
    {
        SustainKeep = (tremoloVibratoRegister & 0x20) != 0;
        if (Mode == OplOperatorMode.Sustain && !SustainKeep)
            Mode = OplOperatorMode.SustainNoKeep;
        else if (Mode == OplOperatorMode.SustainNoKeep && SustainKeep)
            Mode = OplOperatorMode.Sustain;
    }

    private static nint CalculateEnvelopeStepMask(uint rate, uint timeOffset)
    {
        var steps = (rate * 4 + timeOffset) >> 2;
        return (nint)((1u << (int)(steps <= 12 ? 12 - steps : 0)) - 1);
    }

    public void AdvanceOff()
    {
    }

    public void AdvanceSustain()
    {
        var steps = GeneratorPosition / FixedPoint;
        CurrentEnvelopeStep = unchecked(CurrentEnvelopeStep + (nint)steps);
        GeneratorPosition -= steps * FixedPoint;
    }

    public void AdvanceRelease()
    {
        if (Amplitude > ReleaseThreshold)
            Amplitude *= ReleaseMultiplier;

        var steps = GeneratorPosition / FixedPoint;
        for (uint i = 0; i < steps; i++)
        {
            CurrentEnvelopeStep = unchecked(CurrentEnvelopeStep + 1);
            if ((CurrentEnvelopeStep & ReleaseStepMask) != 0)
                continue;

            if (Amplitude <= ReleaseThreshold)
            {
                Amplitude = 0;
                if (Mode == OplOperatorMode.Release)
                    Mode = OplOperatorMode.Off;
            }
            StepAmplitude = Amplitude;
        }

        GeneratorPosition -= steps * FixedPoint;
    }

    public void AdvanceDecay()
    {
        if (Amplitude > SustainLevel)
            Amplitude *= DecayMultiplier;

        var steps = GeneratorPosition / FixedPoint;
        for (uint i = 0; i < steps; i++)
        {
            CurrentEnvelopeStep = unchecked(CurrentEnvelopeStep + 1);
            if ((CurrentEnvelopeStep & DecayStepMask) != 0)
                continue;

            if (Amplitude <= SustainLevel)
            {
                if (SustainKeep)
                {
                    Mode = OplOperatorMode.Sustain;
                    Amplitude = SustainLevel;
                }
                else
                {
                    Mode = OplOperatorMode.SustainNoKeep;
                }
            }
            StepAmplitude = Amplitude;
        }

        GeneratorPosition -= steps * FixedPoint;
    }

    public void AdvanceAttack()
    {
        Amplitude = ((Attack3 * Amplitude + Attack2) * Amplitude + Attack1) * Amplitude + Attack0;

        var steps = GeneratorPosition / FixedPoint;
        for (uint i = 0; i < steps; i++)
        {
            CurrentEnvelopeStep = unchecked(CurrentEnvelopeStep + 1);
            if ((CurrentEnvelopeStep & AttackStepMask) != 0)
                continue;

            if (Amplitude > 1)
            {
                Mode = OplOperatorMode.Decay;
                Amplitude = 1;
                StepAmplitude = 1;
            }

            AttackSkipPosition = unchecked((byte)(AttackSkipPosition << 1));
            if (AttackSkipPosition == 0)
                AttackSkipPosition = 1;
            if ((AttackSkipPosition & AttackSkipMask) != 0)
                StepAmplitude = Amplitude;
        }

        GeneratorPosition -= steps * FixedPoint;
    }
}

public sealed class OplPortState(bool opl3 = false)
{
    private readonly byte[] _registers = new byte[512];

    public byte Status { get; set; }
    public uint RegisterIndex { get; private set; }

    public byte Read(uint port)
    {
        if ((port & 1) != 0)
            return opl3 ? (byte)0 : byte.MaxValue;
        return opl3 ? Status : (byte)(Status | 6);
    }

    public void WriteIndex(uint port, byte value)
    {
        uint index = value;
        if (opl3 && (port & 3) != 0 &&
            ((_registers[0x105] & 1) != 0 || index == 5))
            index |= 0x100;
        RegisterIndex = index;
    }

    public void WriteRegister(uint register, byte value)
    {
        if (register >= _registers.Length) throw new ArgumentOutOfRangeException(nameof(register));
        _registers[register] = value;
    }
}

public sealed partial class LdsPlayer(IOplSynthesizer opl, Func<uint>? nextRandom = null) : IMusicPlayer
{
    private sealed class Instrument(byte[] data)
    {
        public byte ModMisc => data[0];
        public byte ModVolume => data[1];
        public byte ModAttackDecay => data[2];
        public byte ModSustainRelease => data[3];
        public byte ModWave => data[4];
        public byte CarrierMisc => data[5];
        public byte CarrierVolume => data[6];
        public byte CarrierAttackDecay => data[7];
        public byte CarrierSustainRelease => data[8];
        public byte CarrierWave => data[9];
        public byte Feedback => data[10];
        public byte KeyOff => data[11];
        public byte Portamento => data[12];
        public byte Glide => data[13];
        public byte FineTune => data[14];
        public byte Vibrato => data[15];
        public byte VibratoDelay => data[16];
        public byte ModTremolo => data[17];
        public byte CarrierTremolo => data[18];
        public byte TremoloWait => data[19];
        public byte Arpeggio => data[20];
        public ReadOnlySpan<byte> ArpeggioTable => data.AsSpan(21, 12);
    }

    private sealed class Channel
    {
        public ushort TargetTune, LastTune, PackedPosition;
        public byte FineTune, GlideTo, PortamentoSpeed, NextVolume, ModVolume, CarrierVolume,
            VibratoWait, VibratoSpeed, VibratoRate, TremoloStay, ModTremoloWait, ModTremoloSpeed,
            ModTremoloRate, ModTremoloCount, CarrierTremoloWait, CarrierTremoloSpeed, CarrierTremoloRate,
            CarrierTremoloCount, ArpeggioSize, ArpeggioSpeed, KeyCount, VibratoCount, ArpeggioPosition,
            ArpeggioCount, PackedWait, Delay, DelayedSound;
        public ushort DelayedTune;
        public byte[] ArpeggioTable { get; } = new byte[12];
    }

    private readonly record struct Position(ushort Pattern, byte Transpose);
    private Instrument[] _instruments = [];
    private Position[] _positions = [];
    private ushort[] _patterns = [];
    private Channel[] _channels = NewChannels();
    private readonly byte[] _registers = new byte[255];
    private byte[] _delays = new byte[9];
    private byte _tempo, _patternLength, _rhythmRegister, _tempoNow, _row, _fade, _allVolume, _hardFade;
    private ushort _position, _jumpPosition, _mainVolume;
    private byte _jumping;
    public bool Playing { get; private set; }
    public bool SongLooped { get; private set; }
    public byte Row => _row;
    public ushort PositionIndex => _position;
    public byte Tempo => _tempo;
    public byte TempoNow => _tempoNow;
    public byte AllVolume => _allVolume;
    public byte FadeSpeed => _fade;

    private static Channel[] NewChannels() => Enumerable.Range(0, 9).Select(_ => new Channel()).ToArray();

    public void Load(ReadOnlySpan<byte> song)
    {
        if (song.Length < 17) throw new EndOfStreamException("LDS song header is truncated.");
        if (song[0] > 2) throw new InvalidDataException("error: failed to load music");
        var tempo = song[3];
        var length = song[4];
        var delays = song.Slice(5, 9).ToArray();
        var rhythm = song[14];
        var patchCount = BinaryPrimitives.ReadUInt16LittleEndian(song[15..]);
        var cursor = 17;
        var patches = new Instrument[patchCount];
        for (var i = 0; i < patches.Length; i++)
        {
            Require(song, cursor, 46);
            patches[i] = new Instrument(song.Slice(cursor, 46).ToArray());
            cursor += 46;
        }
        Require(song, cursor, 2);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(song[cursor..]);
        cursor += 2;
        var positions = new Position[count * 9];
        for (var i = 0; i < positions.Length; i++)
        {
            Require(song, cursor, 3);
            positions[i] = new Position((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(song[cursor..]) / 2), song[cursor + 2]);
            cursor += 3;
        }
        Require(song, cursor, 2);
        cursor += 2;
        var patterns = new ushort[(song.Length - cursor) / 2];
        for (var i = 0; i < patterns.Length; i++) patterns[i] = BinaryPrimitives.ReadUInt16LittleEndian(song[(cursor + i * 2)..]);
        _instruments = patches; _positions = positions; _patterns = patterns;
        _tempo = tempo; _patternLength = length; _delays = delays; _rhythmRegister = rhythm;
        Rewind();
    }

    private static void Require(ReadOnlySpan<byte> song, int cursor, int count)
    {
        if (cursor > song.Length - count) throw new EndOfStreamException("LDS song data is truncated.");
    }

    public void Free()
    {
        _instruments = []; _positions = []; _patterns = [];
        Playing = false;
    }

    public void Rewind()
    {
        _tempoNow = 3;
        Playing = true; SongLooped = false;
        _jumping = _fade = _allVolume = _hardFade = _row = 0;
        _position = _jumpPosition = _mainVolume = 0;
        _channels = NewChannels();
        Array.Clear(_registers);
        opl.Reset();
        opl.Write(1, 0x20); opl.Write(8, 0); opl.Write(0xBD, _rhythmRegister);
        for (var i = 0; i < 9; i++)
        {
            var register = Operators[i];
            opl.Write((byte)(0x20 + register), 0); opl.Write((byte)(0x23 + register), 0);
            opl.Write((byte)(0x40 + register), 0x3F); opl.Write((byte)(0x43 + register), 0x3F);
            opl.Write((byte)(0x60 + register), 0xFF); opl.Write((byte)(0x63 + register), 0xFF);
            opl.Write((byte)(0x80 + register), 0xFF); opl.Write((byte)(0x83 + register), 0xFF);
            opl.Write((byte)(0xE0 + register), 0); opl.Write((byte)(0xE3 + register), 0);
            opl.Write((byte)(0xA0 + i), 0); opl.Write((byte)(0xB0 + i), 0); opl.Write((byte)(0xC0 + i), 0);
        }
    }

    public void Fade(byte speed) => _fade = speed;

    public void SetRegister(byte register, byte value)
    {
        if (register >= _registers.Length) throw new InvalidDataException("LDS register exceeds the native cache.");
        if (_registers[register] == value) return;
        opl.Write(register, value);
        _registers[register] = value;
    }

    public void SetRegisterMasked(byte register, byte mask, byte value)
    {
        if (register >= _registers.Length) throw new InvalidDataException("LDS register exceeds the native cache.");
        SetRegister(register, (byte)((_registers[register] & mask) | value));
    }

    public void Render(Span<short> samples) => opl.Render(samples, nextRandom);

    private void Register(int register, int value) => SetRegister(checked((byte)register), unchecked((byte)value));
    private void Masked(int register, int mask, int value) => SetRegisterMasked(checked((byte)register), (byte)mask, unchecked((byte)value));

    public void PlaySound(int instrument, int channel, int tune)
    {
        if ((uint)channel >= _channels.Length || (uint)instrument >= _instruments.Length)
            throw new InvalidDataException("LDS note references a missing channel or instrument.");
        var c = _channels[channel];
        var patch = _instruments[instrument];
        var register = Operators[channel];
        unchecked
        {
            tune += ((patch.FineTune + c.FineTune + 0x80) & 0xFF) - 0x80;
            if (patch.Arpeggio == 0)
            {
                var adjustment = patch.ArpeggioTable[0] << 4;
                tune += adjustment > 0x800 ? -(adjustment ^ 0xFF0) - 16 : adjustment;
            }
            if (c.GlideTo != 0)
            {
                c.TargetTune = (ushort)tune; c.PortamentoSpeed = c.GlideTo;
                c.GlideTo = c.FineTune = 0;
                return;
            }
            Register(0x20 + register, patch.ModMisc);
            c.ModVolume = c.NextVolume == 0 || (patch.Feedback & 1) == 0 ? patch.ModVolume
                : (byte)((patch.ModVolume & 0xC0) | (((patch.ModVolume & 0x3F) * c.NextVolume) >> 6));
            Register(0x40 + register, ((patch.Feedback & 1) != 0 && _allVolume != 0
                ? (c.ModVolume & 0xC0) | (((c.ModVolume & 0x3F) * _allVolume) >> 8) : c.ModVolume) ^ 0x3F);
            Register(0x60 + register, patch.ModAttackDecay); Register(0x80 + register, patch.ModSustainRelease);
            Register(0xE0 + register, patch.ModWave);
            Register(0x23 + register, patch.CarrierMisc);
            c.CarrierVolume = c.NextVolume == 0 ? patch.CarrierVolume
                : (byte)((patch.CarrierVolume & 0xC0) | (((patch.CarrierVolume & 0x3F) * c.NextVolume) >> 6));
            Register(0x43 + register, (_allVolume != 0
                ? (c.CarrierVolume & 0xC0) | (((c.CarrierVolume & 0x3F) * _allVolume) >> 8) : c.CarrierVolume) ^ 0x3F);
            Register(0x63 + register, patch.CarrierAttackDecay); Register(0x83 + register, patch.CarrierSustainRelease);
            Register(0xE3 + register, patch.CarrierWave); Register(0xC0 + channel, patch.Feedback);
            Masked(0xB0 + channel, 0xDF, 0);
            if (tune < 0) throw new InvalidDataException("LDS note has a negative native frequency-table index.");
            var frequency = Frequencies[tune % 192];
            var octave = (byte)(tune / 192 - 1);
            if (patch.Glide == 0)
            {
                if (patch.Portamento == 0 || c.LastTune == 0)
                {
                    Register(0xA0 + channel, frequency & 0xFF);
                    Register(0xB0 + channel, (octave << 2) + 0x20 + (frequency >> 8));
                    c.LastTune = c.TargetTune = (ushort)tune;
                }
                else
                {
                    c.TargetTune = (ushort)tune; c.PortamentoSpeed = patch.Portamento;
                    Masked(0xB0 + channel, 0xDF, 0x20);
                }
            }
            else
            {
                Register(0xA0 + channel, frequency & 0xFF);
                Register(0xB0 + channel, (octave << 2) + 0x20 + (frequency >> 8));
                c.LastTune = (ushort)tune;
                c.TargetTune = (ushort)(tune + ((patch.Glide + 0x80) & 0xFF) - 0x80);
                c.PortamentoSpeed = patch.Portamento;
            }
            if (patch.Vibrato == 0) c.VibratoWait = c.VibratoSpeed = c.VibratoRate = 0;
            else
            {
                c.VibratoWait = patch.VibratoDelay;
                c.VibratoSpeed = (byte)((patch.Vibrato >> 4) + 2); c.VibratoRate = (byte)((patch.Vibrato & 15) + 1);
            }
            if ((c.TremoloStay & 0xF0) == 0)
            {
                c.ModTremoloWait = (byte)((patch.TremoloWait & 0xF0) >> 3);
                c.ModTremoloSpeed = (byte)(patch.ModTremolo >> 4); c.ModTremoloRate = (byte)(patch.ModTremolo & 15);
                c.ModTremoloCount = 0;
            }
            if ((c.TremoloStay & 15) == 0)
            {
                c.CarrierTremoloWait = (byte)((patch.TremoloWait & 15) << 1);
                c.CarrierTremoloSpeed = (byte)(patch.CarrierTremolo >> 4); c.CarrierTremoloRate = (byte)(patch.CarrierTremolo & 15);
                c.CarrierTremoloCount = 0;
            }
            c.ArpeggioSize = (byte)(patch.Arpeggio & 15); c.ArpeggioSpeed = (byte)(patch.Arpeggio >> 4);
            patch.ArpeggioTable.CopyTo(c.ArpeggioTable);
            c.KeyCount = patch.KeyOff;
            c.NextVolume = c.GlideTo = c.FineTune = c.VibratoCount = c.ArpeggioPosition = c.ArpeggioCount = 0;
        }
    }
}
