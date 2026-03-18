namespace ScaleSimulator;

internal sealed class WeightSource
{
    private readonly List<ScaleWeightSample> _weights;
    private int _index;

    private WeightSource(List<ScaleWeightSample> weights)
    {
        _weights = weights;
        _index = 0;
    }

    public IReadOnlyList<ScaleWeightSample> AllWeights => _weights;

    public static WeightSource Create(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return FromFile(filePath);
        }

        return FromDefaults();
    }

    public ScaleReading Next(long tareValue)
    {
        if (_weights.Count == 0)
        {
            throw new InvalidOperationException("No hay pesos disponibles para enviar.");
        }

        ScaleWeightSample sample = _weights[_index];
        _index = (_index + 1) % _weights.Count;
        return new ScaleReading(sample.RawWeightText, sample.WeightValue, tareValue);
    }

    private static WeightSource FromDefaults()
    {
        string[] defaults =
        {
            "100000",
            "101000",
            "102000",
            "101500",
            "100500",
            "99500",
            "100250",
            "100750"
        };

        return new WeightSource(defaults.Select(CreateSample).ToList());
    }

    private static WeightSource FromFile(string filePath)
    {
        List<ScaleWeightSample> lines = File.ReadAllLines(filePath)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(CreateSample)
            .ToList();

        if (lines.Count == 0)
        {
            throw new InvalidOperationException($"El archivo '{filePath}' no contiene lineas validas.");
        }

        return new WeightSource(lines);
    }

    private static ScaleWeightSample CreateSample(string line)
    {
        if (!line.All(char.IsDigit))
        {
            throw new InvalidOperationException(
                $"Linea invalida. Solo se permiten numeros enteros en gramos. Valor leido: '{line}'.");
        }

        if (!long.TryParse(line, out long numericValue))
        {
            throw new InvalidOperationException(
                $"No se pudo convertir el peso '{line}' a un valor numerico valido.");
        }

        return new ScaleWeightSample(line, numericValue);
    }
}
