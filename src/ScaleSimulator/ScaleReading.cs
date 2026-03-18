namespace ScaleSimulator;

internal readonly record struct ScaleWeightSample(string RawWeightText, long WeightValue);

internal readonly record struct ScaleReading(string RawWeightText, long WeightValue, long TareValue);
