# ScaleSimulator

Simulador local para pruebas de integracion por puerto serie.

Actualmente cubre dos comportamientos sobre un mismo puerto COM:

- Balanza: envia tramas por el protocolo seleccionado.
- Pulsador: simula la opresion mediante un pulso de 500 ms sobre una linea de control.

## Protocolos de balanza soportados

### `simple-ascii`

- Mantiene el comportamiento historico del simulador.
- Envia el peso como texto ASCII.
- Usa `CRLF` o `LF` segun `--newline`.
- Usa los parametros serie configurados por CLI.

### `w180-t`

- Envia tramas binarias/ASCII de 18 bytes.
- Usa `STX + A + B + C + PESO(6) + TARA(6) + CR + LF`.
- Perfil serie fijo: `9600 / 7E2 / Handshake.None`.
- Ignora `--baud`, `--databits`, `--stopbits`, `--parity` y `--newline`.
- La tara se codifica en la trama y puede cambiarse desde la UI.

## Que simula

### Balanza

- Un valor de peso por envio.
- Transmision continua.
- Intervalo configurable con `--interval-ms`.
- Seleccion de protocolo desde CLI o UI.
- Cambio de protocolo mediante `Stop`, cambio de seleccion y `Start`.

### Pulsador

- No envia ninguna trama.
- No comparte el canal de datos con la balanza.
- Se dispara manualmente desde la mini UI.
- Usa una sola linea de control por vez.
- El pulso dura siempre 500 ms.
- La linea se restablece a reposo al terminar el pulso y tambien al cerrar o detener la comunicacion.

## Ejecucion

Compilar:

```powershell
dotnet build src/ScaleSimulator/ScaleSimulator.csproj
```

Ejecutar balanza en `simple-ascii`:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3
```

Ejecutar balanza en `w180-t` con la mini UI:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3 --ui --scale-protocol w180-t --tare 0
```

Usar `DTR` en lugar de `RTS` para el pulsador:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3 --ui --button-line dtr
```

Usar archivo de pesos y `LF` con `simple-ascii`:

```powershell
dotnet run --project src/ScaleSimulator -- --port COM3 --scale-protocol simple-ascii --file .\samples\weights.txt --interval-ms 1000 --newline lf
```

## Parametros

- `--port`: puerto COM del simulador. Obligatorio.
- `--scale-protocol`: `simple-ascii` o `w180-t`. Default `simple-ascii`.
- `--tare`: tara para protocolos que la soportan. Default `0`.
- `--interval-ms`: intervalo entre envios de balanza. Default `1000`.
- `--file`: archivo con pesos, un numero por linea.
- `--newline`: `crlf` o `lf`. Solo aplica a `simple-ascii`.
- `--ui`: abre la mini UI para pulsador y control de balanza.
- `--button-line`: `rts` o `dtr`. Default `rts`.
- `--baud`: solo aplica a `simple-ascii`. Default `9600`.
- `--databits`: solo aplica a `simple-ascii`. Default `8`.
- `--stopbits`: solo aplica a `simple-ascii`. Default `1`.
- `--parity`: solo aplica a `simple-ascii`. Default `None`.

## Controles de la UI

- Selector de protocolo de balanza.
- Boton `Start` para iniciar la comunicacion con el protocolo seleccionado.
- Boton `Stop` para detener la comunicacion y habilitar el cambio de protocolo.
- Campo `Tara` para protocolos que la soportan.
- Resumen del perfil serie efectivo.
- Boton `Pulsar 500 ms` para el pulsador.

## Flujo de prueba recomendado

1. Levantar el simulador con `--ui`.
2. Configurar la aplicacion de pesajes con el mismo protocolo y perfil serie efectivo.
3. Verificar que la app siga recibiendo la balanza con el protocolo elegido.
4. Si se quiere cambiar de protocolo, presionar `Stop`, elegir el nuevo protocolo y luego `Start`.
5. Presionar `Pulsar 500 ms` en la UI.
6. Verificar que la app detecte la opresion del pulsador sin esperar ninguna trama adicional.

## Guia para la app de pesajes

La guia principal para el agente que desarrolla la aplicacion esta en [integracion-app-pesajes.md](./integracion-app-pesajes.md).
