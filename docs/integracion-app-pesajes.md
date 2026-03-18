# Integracion con la aplicacion de pesajes

## Objetivo

Este documento define el contrato de integracion entre la aplicacion de pesajes y este simulador para ambos canales:

- balanza por tramas
- pulsador por lineas de control

La app puede usar el simulador para probar la lectura actual en `simple-ascii` o para avanzar sobre la lectura de produccion en `w180-t`.

## Contrato general

- Hay un unico puerto serie compartido por balanza y pulsador.
- La balanza usa el canal de datos.
- El pulsador usa lineas de control.
- La app debe mantener un unico `SerialPort` abierto para ambos comportamientos.

## Protocolos de balanza disponibles

### `simple-ascii`

- Es el protocolo historico del simulador.
- Envia el peso como ASCII sin punto decimal.
- Termina en `CRLF` o `LF`.
- Usa los parametros serie configurados en CLI.

Ejemplo:

```text
100000\r\n
101000\r\n
102000\r\n
```

### `w180-t`

- Es el protocolo objetivo de produccion.
- Opera en transmision continua.
- Usa longitud fija de 18 bytes.
- No requiere comandos desde el host.
- Perfil serie fijo del simulador:
  - baudrate `9600`
  - data bits `7`
  - parity `Even`
  - stop bits `2`
  - handshake `None`

Estructura:

```text
<STX><A><B><C><PESO><TARA><CR><LF>
```

Detalle de la implementacion actual del simulador:

- `STX = 0x02`
- `A = 0x00`
- `B = 0x01`
- `C = 0x00`
- `PESO = 6 bytes ASCII`
- `TARA = 6 bytes ASCII`
- `CR = 0x0D`
- `LF = 0x0A`

Interpretacion del estado emitido por el simulador en `w180-t`:

- peso neto
- estable
- positivo
- en rango
- sin tecla informada en byte `C`

Ejemplo de trama para peso `1234` y tara `0`:

```text
02 00 01 00 30 30 31 32 33 34 30 30 30 30 30 30 0D 0A
```

## Pulsador

- El pulsador no envia texto.
- El pulsador no emite tramas.
- La opresion se representa como un pulso de 500 ms sobre una linea de control.
- La seleccion se hace al iniciar el simulador con `--button-line rts|dtr`.

Contrato de prueba recomendado:

| Simulador (`--button-line`) | Aplicacion debe leer | API tipica en .NET |
| --- | --- | --- |
| `rts` | `CTS` | `SerialPort.CtsHolding` y `SerialPinChange.CtsChanged` |
| `dtr` | `DSR` | `SerialPort.DsrHolding` y `SerialPinChange.DsrChanged` |

## Que debe hacer la aplicacion

La app no debe intentar leer el pulsador desde el parser de la balanza.

La app debe:

1. Abrir un unico `SerialPort`.
2. Elegir el parser de balanza segun el protocolo configurado.
3. Seguir observando la linea de control configurada para detectar el pulsador.
4. Publicar eventos de negocio distintos para lectura de balanza y opresion del pulsador.

## Recomendacion de modelado

Separar dos decisiones de configuracion:

```csharp
public enum ScaleProtocolKind
{
    SimpleAscii,
    W180T
}

public enum ButtonInputLine
{
    Cts,
    Dsr
}
```

Y mantener un adaptador serie unico que sea dueno de:

- el `SerialPort`
- el parser de balanza activo
- la deteccion del pulsador por `PinChanged`

## Lectura sugerida de `w180-t`

La app deberia:

1. Sincronizar por `STX` (`0x02`).
2. Leer la trama completa de 18 bytes.
3. Verificar `CR` y `LF` al final.
4. Interpretar los bytes `A/B/C`.
5. Convertir `PESO` y `TARA` de ASCII a numero entero.
6. Aplicar la escala decimal configurada por la aplicacion o por el equipo real.

Campos utiles para la app:

- `B bit 0`: peso neto
- `B bit 1`: negativo
- `B bit 2`: fuera de rango
- `B bit 3`: inestable

La implementacion actual del simulador emite esos flags en estado estable y valido, salvo que se amplie mas adelante.

## Ejemplo de implementacion en .NET

```csharp
using System.IO.Ports;

public enum ScaleProtocolKind
{
    SimpleAscii,
    W180T
}

public enum ButtonInputLine
{
    Cts,
    Dsr
}

public sealed class WeighingSerialAdapter : IDisposable
{
    private readonly SerialPort _serialPort;
    private readonly ScaleProtocolKind _scaleProtocol;
    private readonly ButtonInputLine _buttonInputLine;
    private bool _lastButtonState;

    public event Action<int>? WeightReceived;
    public event Action? ButtonPressed;

    public WeighingSerialAdapter(
        string portName,
        ScaleProtocolKind scaleProtocol,
        ButtonInputLine buttonInputLine)
    {
        _scaleProtocol = scaleProtocol;
        _buttonInputLine = buttonInputLine;

        _serialPort = scaleProtocol switch
        {
            ScaleProtocolKind.W180T => new SerialPort
            {
                PortName = portName,
                BaudRate = 9600,
                DataBits = 7,
                Parity = Parity.Even,
                StopBits = StopBits.Two,
                Handshake = Handshake.None
            },
            _ => new SerialPort
            {
                PortName = portName,
                BaudRate = 9600,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                NewLine = "\r\n"
            }
        };
    }

    public void Open()
    {
        _serialPort.PinChanged += OnPinChanged;
        _serialPort.DataReceived += OnDataReceived;
        _serialPort.Open();
        _lastButtonState = ReadButtonState();
    }

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        if (_scaleProtocol == ScaleProtocolKind.W180T)
        {
            TryReadW180TFrame();
            return;
        }

        string line = _serialPort.ReadLine();
        if (int.TryParse(line.Trim(), out int weight))
        {
            WeightReceived?.Invoke(weight);
        }
    }

    private void TryReadW180TFrame()
    {
        while (_serialPort.BytesToRead >= 18)
        {
            if (_serialPort.ReadByte() != 0x02)
            {
                continue;
            }

            byte[] buffer = new byte[17];
            int read = _serialPort.Read(buffer, 0, buffer.Length);
            if (read < buffer.Length)
            {
                return;
            }

            if (buffer[15] != 0x0D || buffer[16] != 0x0A)
            {
                continue;
            }

            string weightAscii = System.Text.Encoding.ASCII.GetString(buffer, 3, 6);
            if (int.TryParse(weightAscii, out int weight))
            {
                WeightReceived?.Invoke(weight);
            }
        }
    }

    private void OnPinChanged(object? sender, SerialPinChangedEventArgs e)
    {
        bool isRelevantChange =
            (_buttonInputLine == ButtonInputLine.Cts && e.EventType == SerialPinChange.CtsChanged) ||
            (_buttonInputLine == ButtonInputLine.Dsr && e.EventType == SerialPinChange.DsrChanged);

        if (!isRelevantChange)
        {
            return;
        }

        bool currentState = ReadButtonState();

        if (currentState && !_lastButtonState)
        {
            ButtonPressed?.Invoke();
        }

        _lastButtonState = currentState;
    }

    private bool ReadButtonState()
    {
        return _buttonInputLine switch
        {
            ButtonInputLine.Cts => _serialPort.CtsHolding,
            ButtonInputLine.Dsr => _serialPort.DsrHolding,
            _ => false
        };
    }

    public void Dispose()
    {
        _serialPort.PinChanged -= OnPinChanged;
        _serialPort.DataReceived -= OnDataReceived;

        if (_serialPort.IsOpen)
        {
            _serialPort.Close();
        }

        _serialPort.Dispose();
    }
}
```

## Como probar con este simulador

### Escenario 1: `simple-ascii`

Ejecutar:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3 --scale-protocol simple-ascii
```

Validar:

- La app recibe pesos por parser de linea.
- El pulsador sigue probandose con la UI si se usa `--ui`.

### Escenario 2: `w180-t`

Ejecutar:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3 --ui --scale-protocol w180-t --tare 0
```

Validar:

- La app abre el puerto en `9600 / 7E2`.
- La app sincroniza por `STX`.
- La app recibe tramas de 18 bytes.
- La app interpreta `PESO` y `TARA`.
- La app no depende de `ReadLine()` para este protocolo.

### Escenario 3: cambio de protocolo

En la UI:

1. Presionar `Stop`.
2. Cambiar el protocolo en la lista.
3. Presionar `Start`.

Validar:

- El puerto se reabre con el perfil serie efectivo del protocolo nuevo.
- La app debe cambiar su parser/configuracion en consecuencia.

### Escenario 4: pulsador

Con cualquiera de los protocolos de balanza:

1. Configurar el pulsador del simulador con `--button-line rts` o `dtr`.
2. Hacer clic en `Pulsar 500 ms`.

Validar:

- La balanza sigue comunicando con el protocolo activo.
- La app detecta la opresion por `CTS` o `DSR`.
- No existe trama de pulsador.

## Criterios de aceptacion para la app

- La app mantiene un unico puerto abierto para balanza y pulsador.
- La app selecciona el parser de balanza segun el protocolo configurado.
- En `simple-ascii`, la app puede seguir leyendo por linea.
- En `w180-t`, la app lee 18 bytes por trama y sincroniza por `STX`.
- El pulsador se detecta por lineas de control y no por texto.
- La app puede probar balanza y pulsador en paralelo con este simulador.

## Resumen operativo

- `simple-ascii`: parser de texto por linea.
- `w180-t`: parser binario/ASCII de 18 bytes con `STX` inicial.
- Pulsador: leer una linea de control del mismo puerto.
- `--button-line rts` implica leer `CTS`.
- `--button-line dtr` implica leer `DSR`.
- La opresion se detecta por flanco ascendente.
