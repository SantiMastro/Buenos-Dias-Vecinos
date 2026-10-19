# Dos bugs de la v17 que encontraron las pruebas de la balanza

Encontrados con `test_balanza.py` (carpeta `Assets/PruebasBalanza~/` del repo Buenos-Dias-Vecinos),
corriendo `code_felpudo_v17.py` (20:46) contra un arnés propio: placa, HX711, teclado y reloj
simulados. Felpudo vacío, robustez y control con la v16: todo pasa, sin falsos positivos.
Fallan estos dos.

## Bug 1 — dos personas que suben casi juntas: carga fantasma y OCUPADO trabado (grave)

Si la segunda persona sube **0,6–0,8 s** después que la primera (en el medio de la fase
"midiendo" de la primera), se la cuenta casi una vez y media. Cuando se bajan las dos queda
carga fantasma ≥ 15 kg: **nunca manda BAJO** hasta que se toque Bloq Num. Pasó en 12 de 24
casos (separación 0,6 s: 6/8; 0,8 s: 6/8).

Traza (semilla 1051, separación 0,6 s; `**` = verdad, `>>` = tecla que le llegó a la PC):

```
10.000 s  ** sube una persona de 86 kg
10.166 s     subida de 39 kg (umbral 15), confirmando...
10.355 s     SUBIO   (rápido) carga 90 kg
10.355 s  >> TECLA S
10.600 s  ** sube una persona de 120 kg
11.110 s     ajuste (medido 207 kg): carga 207 kg, sigue OCUPADO     <- MEDIR ya incluye a la segunda
11.296 s     ajuste (rápido): carga 262 kg, sigue OCUPADO            <- y la vuelve a sumar
12.241 s     ajuste (medido 55 kg): carga 262 kg, sigue OCUPADO      <- real: 86 + 120 = 206 kg
24.156 s  ** se baja la persona de 86 kg
24.505 s     ajuste (rápido): carga 174 kg, sigue OCUPADO
25.258 s     ajuste (medido 87 kg): carga 175 kg, sigue OCUPADO
28.751 s  ** se baja la persona de 120 kg
29.032 s     ajuste (rápido): carga 58 kg, sigue OCUPADO
29.881 s     ajuste (medido 116 kg): carga 59 kg, sigue OCUPADO      <- felpudo vacío, nunca manda B
```

**Diagnóstico.** El evento de la primera arranca en t0 = 10,166 s y MEDIR toma las muestras de
t0 + 450…850 ms (10,616–11,016 s). La rampa de la segunda arranca a los 10,600 s, así que
"medido 207 kg" ya trae casi entera a la segunda. Después, `borrar_antes(e["t0"] + MEDIR[0])`
(v17:266) deja como base nueva muestras que incluyen el *principio* de la rampa de la segunda. El
paso siguiente detecta la segunda contra esa base baja y suma otros +55 kg (v17:256–267, sin
chequeo de que las muestras de MEDIR estén quietas).

**Arreglo posible.** En la fase "midiendo", antes de usar `mediana(vals)`, mirar la dispersión
de las muestras de MEDIR (y de las que llegaron después). Si `max − min > umbral`, hay otro
escalón en curso. En ese caso no corregir la carga con esa medición (quedarse con la
provisoria) y cerrar el evento con una base que termine **antes** del escalón nuevo, así se
detecta entero y una sola vez.

## Bug 2 — BAJO llega tarde (1,02–1,17 s) con gente pesada y con toques rápidos

La mediana normal es 350 ms. Pasó en 19 casos: personas de 100–120 kg, 150 kg, y toques de
0,3–1,5 s.

Traza (semilla 1150, persona de 117 kg):

```
47.313 s  ** se baja la persona de 117 kg
47.538 s     bajada de 54 kg (umbral 15), confirmando...
47.634 s     ajuste (rápido): carga 20 kg, sigue OCUPADO   <- la vía rápida mide solo parte de la rampa
48.483 s     BAJO    (medido 112 kg) carga 0 kg            <- 1170 ms después de empezar a bajarse
48.483 s  >> TECLA B
```

**Diagnóstico.** `chequear_rapido()` (v17:224–237) compromete `mediana(difs)` con las muestras de
"ahora", que todavía están en medio de la rampa de bajada. Con alguien de 100 kg o más, lo que
queda (20 kg) sigue arriba de CARGA_MIN_KG = 15, entonces queda OCUPADO hasta que mide a los
850 ms.

Variante con un toque de 0,3 s (semilla 1107), que ya llega a ser frágil:

```
25.000 s  ** sube una persona de 92 kg
25.334 s     SUBIO   (rápido) carga 91 kg
25.334 s  >> TECLA S
25.527 s  ** se baja la persona de 92 kg
25.996 s     ajuste (medido 16 kg): carga 16 kg, sigue OCUPADO   <- MEDIR agarró la cola de la bajada
26.278 s     bajada de 48 kg (umbral 15), confirmando...         <- contra una base contaminada: sale de casualidad
26.656 s     BAJO    (confirmado) carga 0 kg                      <- 1129 ms
26.656 s  >> TECLA B
```

**Arreglo posible.**
- Para bajadas por la vía rápida: si `cuanto >= 0,6 × carga_antes` (o si lo que queda es
  menor que 2 × CARGA_MIN), comprometer como bajada total (carga 0, VACIO). La medición de los
  850 ms ya corrige si alguien quedó arriba.
- El mismo chequeo de dispersión del bug 1 evita el "medido 16 kg" del toque rápido.

## Cruce con el simulador (`sim/resultados.md`)

- **Bug 1 = su problema 1**: mismo mecanismo (`borrar_antes` deja una base que no coincide
  con lo medido). Lo confirman las dos herramientas por separado.
- **Bug 2** no aparece como bug en el simulador porque empareja teclas hasta 3 s. Sus números
  lo muestran igual: p95 de 1,16 s con 150 kg (H_150) y de 1,21 s con dos tiempos (E_2t).
- **Agregué dos pruebas** por cosas que encontró el simulador y mis pruebas no cubrían. Las dos
  fallan con la v17:
  - `test_usb_se_cae_con_la_tecla_apretada_y_se_baja` (su problema 3): S apretada para siempre
    en 8 de 8 casos.
  - `test_corrimientos_bajo_el_umbral_con_ruido_alto` (su D_sd2): S falsa en 3 de 8.
- **`code_felpudo_v17_propuesta.py` contra esta suite: 32 de 33 pruebas pasan.** Arregla los
  bugs 1 y 2 (latencia máxima 545 ms; la regla del 25 % corta la bajada parcial) y la tecla
  apretada. Solo falla `test_corrimientos_bajo_el_umbral_con_ruido_alto` (3 de 8 semillas: un
  corrimiento de 5–12 kg con 2 kg de ruido da una S falsa, y en dos casos después una B por
  `MAX_OCUPADO_S`). El `CARGA_MIN_KG = 25` opcional del simulador apunta justo a eso.

## Cómo reproducir y validar un arreglo

Solo lee el firmware; no toca F:\ ni el puerto serie. El tope de 1 s es `LATENCIA_MAX_MS`, en
`test_balanza.py`.

```
py -I -m unittest discover -s "C:\Users\Iñaki\Documents\GitHub\Buenos-Dias-Vecinos\Assets\PruebasBalanza~" -v
```

Para probar otra versión: `FELPUDO_CODE=<ruta del .py>`. Las pruebas de estos bugs son
`test_dos_personas_suben_casi_juntas`, `test_dos_personas_que_se_pisan`, `test_personas_pesadas`,
`test_persona_pesada_con_crudo_negativo`, `test_toques_rapidos`, `test_una_persona_por_vez`,
`test_usb_se_cae_con_la_tecla_apretada_y_se_baja` y `test_corrimientos_bajo_el_umbral_con_ruido_alto`.
