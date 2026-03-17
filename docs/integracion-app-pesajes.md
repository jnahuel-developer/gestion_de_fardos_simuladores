# Integracion con la aplicacion de pesajes

## Objetivo

Este documento define el contrato de integracion entre la aplicacion de pesajes y este simulador, con foco en el pulsador.

La lectura de balanza ya existe en la app. Lo que falta incorporar es la lectura del pulsador sin usar tramas, porque el pulsador se representara exclusivamente mediante lineas de control del puerto serie.

## Contrato de comunicacion

### Balanza

- La balanza se sigue leyendo por la linea de datos del mismo puerto serie.
- El simulador envia gramos en ASCII.
- Cada envio termina en `CRLF` o `LF`, segun la configuracion elegida al iniciar el simulador.
- El pulsador no modifica este flujo.

Ejemplos de payload de balanza:

```text
100000\r\n
101000\r\n
102000\r\n
```

### Pulsador

- El pulsador no envia texto.
- El pulsador no emite tramas.
- La opresion se representa como un pulso de 500 ms sobre una linea de control.
- El simulador usa una sola linea por vez: `RTS` o `DTR`.
- La seleccion se hace al iniciar el simulador con `--button-line rts|dtr`.
- Terminados los 500 ms, la linea vuelve a reposo.

## Que debe hacer la aplicacion

La aplicacion no debe intentar leer el pulsador desde `Read`, `ReadLine`, `ReadExisting` ni desde el parser de tramas de balanza.

La aplicacion debe:

1. Mantener un unico `SerialPort` abierto para balanza y pulsador.
2. Seguir procesando la balanza exactamente igual que hoy.
3. Agregar la observacion de una linea de entrada del puerto para detectar la opresion del pulsador.
4. Traducir la transicion de reposo a activo en un evento de negocio del tipo `PulsadorPresionado`.

## Mapeo esperado de lineas

El simulador controla una linea de salida local. La aplicacion debe leer la linea de entrada remota correspondiente.

Contrato de prueba recomendado:

| Simulador (`--button-line`) | Aplicacion debe leer | API tipica en .NET |
| --- | --- | --- |
| `rts` | `CTS` | `SerialPort.CtsHolding` y `SerialPinChange.CtsChanged` |
| `dtr` | `DSR` | `SerialPort.DsrHolding` y `SerialPinChange.DsrChanged` |

Notas:

- Este es el contrato recomendado para las pruebas con este simulador.
- Si el hardware definitivo expone otra linea de entrada equivalente, la abstraccion de la app debe cambiar solo en el mapeo, no en la logica.
- La app debe ser lectora de la senal del pulsador. No debe intentar enviar una trama para "confirmar" la opresion.

## Diseno sugerido en la app

### Recomendacion de modelado

Agregar una configuracion explicita para la linea del pulsador, por ejemplo:

```csharp
public enum ButtonInputLine
{
    Cts,
    Dsr
}
```

Y mantener un unico componente dueno del `SerialPort`, por ejemplo:

- lectura de balanza por datos
- lectura de pulsador por linea de control
- publicacion de eventos hacia la capa de aplicacion

### Reglas de implementacion

- Abrir un solo `SerialPort`.
- Configurarlo con los mismos parametros que usa hoy la balanza.
- Suscribirse a `PinChanged`.
- Leer el estado real de la linea al recibir el cambio.
- Detectar solo el flanco ascendente para representar la opresion.
- No generar multiples eventos mientras la linea sigue activa durante el mismo pulso de 500 ms.
- Si tambien se necesita "liberacion", resolverla como un segundo evento basado en el flanco descendente, pero eso no es requisito para usar este simulador.

### Ejemplo de implementacion en .NET

```csharp
using System.IO.Ports;

public enum ButtonInputLine
{
    Cts,
    Dsr
}

public sealed class WeighingSerialAdapter : IDisposable
{
    private readonly SerialPort _serialPort;
    private readonly ButtonInputLine _buttonInputLine;
    private bool _lastButtonState;

    public event Action? ButtonPressed;

    public WeighingSerialAdapter(string portName, ButtonInputLine buttonInputLine)
    {
        _buttonInputLine = buttonInputLine;
        _serialPort = new SerialPort
        {
            PortName = portName,
            BaudRate = 9600,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            NewLine = "\r\n"
        };
    }

    public void Open()
    {
        _serialPort.PinChanged += OnPinChanged;
        _serialPort.DataReceived += OnDataReceived;
        _serialPort.Open();
        _lastButtonState = ReadButtonState();
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

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        // Mantener aqui la lectura actual de la balanza.
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

## Comportamiento esperado en la aplicacion

Una implementacion correcta deberia cumplir esto:

- La lectura de balanza sigue funcionando sin cambios de protocolo.
- La lectura del pulsador no depende del canal de datos.
- Una opresion manual en la UI del simulador produce un unico evento de "presionado".
- Durante el pulso del pulsador, la balanza sigue entregando pesos normalmente.
- Cerrar y reabrir el puerto reinicia el estado base del pulsador en reposo.

## Como probar con este simulador

### Escenario 1: balanza solamente

Ejecutar:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3
```

Validar:

- La app recibe pesos.
- La app no necesita cambios para este caso.

### Escenario 2: balanza + pulsador por `RTS`

Ejecutar:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3 --ui --button-line rts
```

Configurar la app para leer el pulsador desde `CTS`.

Validar:

- La balanza sigue entregando pesos.
- Al hacer clic en `Pulsar 500 ms`, la app detecta una opresion.
- La app no espera una trama adicional.
- La app no genera multiples opresiones durante el mismo pulso.

### Escenario 3: balanza + pulsador por `DTR`

Ejecutar:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3 --ui --button-line dtr
```

Configurar la app para leer el pulsador desde `DSR`.

Validar:

- El comportamiento es equivalente al escenario anterior.
- Solo cambia la linea de entrada observada por la app.

## Criterios de aceptacion para el agente que implementa la app

- La app mantiene un unico puerto abierto para balanza y pulsador.
- La balanza sigue parseandose por tramas ASCII.
- El pulsador se detecta por lineas de control y no por texto.
- El mapping entre linea simulada y linea leida esta explicitado en configuracion o en codigo.
- El evento de opresion se produce una sola vez por pulso de 500 ms.
- El simulador puede usarse para probar ambos comportamientos al mismo tiempo.

## Resumen operativo

Si el agente que desarrolla la app necesita una regla corta para implementar:

- Balanza: seguir leyendo texto por datos.
- Pulsador: leer una linea de control del mismo puerto.
- `--button-line rts` en el simulador implica leer `CTS` en la app.
- `--button-line dtr` en el simulador implica leer `DSR` en la app.
- La opresion se detecta por flanco ascendente.
- No existe trama de pulsador.
