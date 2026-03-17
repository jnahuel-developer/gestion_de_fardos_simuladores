# ScaleSimulator

Simulador local para pruebas de integracion por puerto serie.

Actualmente cubre dos comportamientos sobre un mismo puerto COM:

- Balanza: envia pesos ASCII por la linea de datos.
- Pulsador: simula la opresion mediante un pulso de 500 ms sobre una linea de control.

## Que simula

### Balanza

- Un valor de peso en gramos por envio.
- Texto ASCII.
- Un valor por linea.
- `CRLF` o `LF` segun `--newline`.
- Envio periodico segun `--interval-ms`.

### Pulsador

- No envia ninguna trama.
- No comparte el canal de datos con la balanza.
- Se dispara manualmente desde la mini UI.
- Usa una sola linea de control por vez.
- El pulso dura siempre 500 ms.
- La linea se restablece a reposo al terminar el pulso y tambien al cerrar el simulador.

## Ejecucion

Compilar:

```powershell
dotnet build src/ScaleSimulator/ScaleSimulator.csproj
```

Ejecutar solo balanza:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3
```

Ejecutar balanza + mini UI para el pulsador:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3 --ui
```

Usar `DTR` en lugar de `RTS` para el pulsador:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3 --ui --button-line dtr
```

Usar archivo de pesos y `LF`:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3 --file .\samples\weights.txt --interval-ms 1000 --newline lf
```

## Parametros

- `--port`: puerto COM del simulador. Obligatorio.
- `--interval-ms`: intervalo entre envios de balanza. Default `1000`.
- `--file`: archivo con pesos, un numero por linea.
- `--newline`: `crlf` o `lf`. Default `crlf`.
- `--ui`: abre la mini UI para disparar el pulsador.
- `--button-line`: `rts` o `dtr`. Default `rts`.
- `--baud`: default `9600`.
- `--databits`: default `8`.
- `--stopbits`: default `1`.
- `--parity`: default `None`.

## Flujo de prueba recomendado

1. Levantar el simulador con `--ui`.
2. Configurar la aplicacion de pesajes con los mismos parametros serie.
3. Verificar que la app siga recibiendo pesos desde la balanza simulada.
4. Presionar `Pulsar 500 ms` en la UI.
5. Verificar que la app detecte la opresion del pulsador sin esperar ninguna trama adicional.

## Guia para la app de pesajes

La guia principal para el agente que desarrolla la aplicacion esta en [integracion-app-pesajes.md](./integracion-app-pesajes.md).
