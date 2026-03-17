namespace ScaleSimulator;

internal sealed class WeightSource
{
    private readonly List<string> _weights;
    private int _index;

    private WeightSource(List<string> weights)
    {
        _weights = weights;
        _index = 0;
    }

    public static WeightSource Create(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return FromFile(filePath);
        }

        return FromDefaults();
    }

    public string Next()
    {
        if (_weights.Count == 0)
        {
            throw new InvalidOperationException("No hay pesos disponibles para enviar.");
        }

        string value = _weights[_index];
        _index = (_index + 1) % _weights.Count;
        return value;
    }

    private static WeightSource FromDefaults()
    {
        var defaults = new List<string>
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

        return new WeightSource(defaults);
    }

    private static WeightSource FromFile(string filePath)
    {
        var lines = File.ReadAllLines(filePath)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (lines.Count == 0)
        {
            throw new InvalidOperationException($"El archivo '{filePath}' no contiene líneas válidas.");
        }

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];

            if (!line.All(char.IsDigit))
            {
                throw new InvalidOperationException(
                    $"Línea inválida en '{filePath}' (línea {i + 1}). Solo se permiten números enteros en gramos. Valor leído: '{line}'.");
            }
        }

        return new WeightSource(lines);
    }
}