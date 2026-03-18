namespace ScaleSimulator;

internal static class ScaleProtocolCatalog
{
    private static readonly IScaleProtocol[] _all =
    {
        new SimpleAsciiScaleProtocol(),
        new W180TScaleProtocol()
    };

    public static IReadOnlyList<IScaleProtocol> All => _all;

    public static IScaleProtocol Get(ScaleProtocolKind kind)
    {
        foreach (IScaleProtocol protocol in _all)
        {
            if (protocol.Kind == kind)
            {
                return protocol;
            }
        }

        throw new InvalidOperationException($"No hay un protocolo registrado para '{kind}'.");
    }
}
