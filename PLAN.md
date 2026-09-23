# BUENOS DÍAS, VECINO — Plan de implementación

Juego 2D de scroll lateral, un solo botón, Unity 6 (6000.5.3f1) + URP 2D.
Destino: PC y gabinete de arcade físico.

Este archivo es la fuente de verdad del proyecto. El chat se compacta; esto no.
**Se actualiza al terminar cada fase.**

> ## ⚠️ El proyecto se mudó de repo (23/9)
>
> El repo vivo es **este**, `C:\Users\Santi\GitHub\Buenos Dias Vecinos`, con remoto en
> GitHub. El de `Documents\Unity Projects\Buenos Dias Vecinos` quedó **congelado** y no se
> toca más.
>
> El 9/9 se copió el proyecto acá y se subió como repo nuevo. Vino todo el código, pero
> **quedaron afuera este PLAN y la historia de git** —más de 60 commits con el porqué de cada
> decisión—. Este archivo se trajo a mano. La historia sigue en el repo viejo, que no comparte
> ni un commit con este: si hace falta el razonamiento detrás de un cambio viejo, está en el
> `git log` de allá.
>
> **Lo que quedó desactualizado en este PLAN:** todo lo de §13bis y §15.3 sobre las tres
> modalidades de input. El 12/9 el input se reescribió a **un solo esquema de dos botones**
> —A = timbre (click izquierdo), B = felpudo (Espacio, desde una Raspberry Pi), con
> `InputConfig`—. Desaparecieron el modo un botón, el modo Objeto y **el libro como entrada
> aparte**: el QTE se juega con A. El resto del PLAN vale.
>
> Lo que se sumó el 12/9 y todavía no está documentado acá: highscore con carga de iniciales,
> humo de chimeneas, siluetas en las ventanas y el `TwoButtonArbiter`.

---

## 1. Constantes del proyecto

| Constante | Valor | Nota |
|---|---|---|
| Pixels Per Unit | 32 | lo fuerza `Assets/Editor/SpriteImportSettings.cs` |
| Resolución de referencia | 384 × 216 | 12 × 6.75 unidades de mundo |
| Orthographic Size | 3.375 | = 216 / 2 / 32 |
| Color space | Linear | |
| Input | solo Input System nuevo (`activeInputHandler: 1`) | |

Conversión px → unidades: dividir por 32. Toda velocidad y distancia de la spec
está en px; los ScriptableObjects las exponen en px y las convierten internamente.

**Regla de escala:** nunca escalas no enteras en pixel art. Si hay que escalar, ×2 o ×3.

---

## 2. Arquitectura

| Decisión | Problema que resuelve |
|---|---|
| FSM explícita para el run loop | La spec garantiza que los estados nunca se solapan. Con flags eso depende de no equivocarse; con una FSM que despacha el botón al estado activo, la ambigüedad es inexpresable. |
| ScriptableObjects como base de datos | Criterios 9 y 10: agregar señal o religión sin tocar código. El código itera listas de assets, nunca enumera. |
| Canales de evento como SO | "La UI escucha al juego" + "sin `FindObjectOfType`" + "sin singletons". El juego levanta el evento sobre un asset; la UI se suscribe al mismo asset. Ninguno conoce al otro. |
| Simulación en clases planas de C# | Pity, skillcheck y generación son funciones puras. Fuera de `MonoBehaviour` se testean sin entrar en Play Mode y se pueden simular 8000 partidas en milisegundos. |
| Composición en el prefab de casa | Criterio 4: abrir el prefab y cambiar una ventana. La casa es un armazón con slots, no una jerarquía fija. |
| Pool por distancia, no por conteo | Con anchos y separaciones variables, contar casas da resultados distintos según la generación; medir distancia detrás de cámara no. |
| Sin service locator | `GameRoot` posee los sistemas y los cablea por Inspector en `Awake()`. Los canales de evento ya son assets, así que no hace falta acceso global. |

---

## 3. Decisiones cerradas con el diseñador

Estas reemplazan lo que decía la spec original.

### 3.1 Techos — composición, no escalado
Tres tipos en `HouseGenConfig`:
- `LosaCompleta` — `env_techo_losa` tileado en todo el ancho.
- `FrenteDosAguas` — `env_techo_dosaguas` a **tamaño nativo 128px, centrado sobre la
  puerta**, con losa tileada a los costados. Es cómo se ven las casas del conurbano.
- `DosAguasCompleto` — solo si la casa mide ≤ 128px.

Cero distorsión, cero 9-slice, cero escala fraccionaria.

### 3.2 Escala por comitiva — continua
Las **fórmulas** son la fuente de verdad. La tabla de la spec era una muestra en los
bordes de cada tramo.

```
velocidad = clamp(1 + F * 0.035, 1, 1.75)     continua
ancho     = clamp(1 - F * 0.030, 0.45, 1)     continua
eslabones = por tramo (0-3→1, 4-7→2, 8-11→3, 12+→4)   entero, no puede ser continuo
```
Toggle continuo/tramos implementado; **default: continuo**.

### 3.3 Anti-racha — el pity nunca hace mentir a las señales
Reemplaza la regla original. Leer señales es la habilidad central del juego;
castigarla estaría mal.

```
1. Pity de generación (principal)
   Con emptyRun >= 1, sesgar las casas NUEVAS hacia señales positivas.
   Cuanto más alto el pity, más amigable se pone el barrio.
   Las señales siguen diciendo la verdad.

2. Pity de tirada (acotado)
   Solo se aplica a casas con chance propio >= 0.42.
   Por debajo de ese umbral el pity vale 0.

3. Garantía dura (acotada igual)
   emptyRun >= 3 fuerza ocupación SOLO en casas con chance >= 0.42.
   Insistir con casas que se ven muertas sigue saliendo vacío. Enseña a leer.
```

#### ✅ Verificación hecha en fase 5 — 8000 partidas × 25 casas

Es la primera corrida con **el lazo cerrado**: hasta la fase 4 `HouseSpawner.EmptyRun`
devolvía 0 fijo, así que el sesgo de generación —el mecanismo *principal*— nunca se
activaba. Ahora el spawner tiene un `PityTracker` de verdad y el resolver lo alimenta.

| jugador | timbrazos | abrieron | chance media | peor racha |
|---|---|---|---|---|
| toca TODAS las casas | 200.000 | **55.7%** | 0.4853 | **3** |
| lee y saltea las muertas | 133.976 | **66.8%** | 0.6061 | **3** |

**Peor racha 3 en los dos modos, y CERO rachas de 4** (223 y 386 rachas de 3 sobre 8000
partidas). El objetivo de diseño se cumple sin tocar el incremento.

El número que más importa no es la racha sino la brecha: **leer las señales sube la tasa
de puertas abiertas de 55.7% a 66.8%, casi 11 puntos.** Eso es lo que hace que la
habilidad central del juego valga la pena, y es medible.

El sesgo de generación se activa en el 42.6% de las casas del modo "toca todas". Antes de
esta fase se activaba en el 0%.

### 3.4 Castigo de comitiva — baja de tramo
Fallar con 5+ seguidores baja la comitiva **al tope del tramo inferior**:
F=10 → 7, F=6 → 3. Modos `QuitarUno` y `BajarDeTramo`; **default: `BajarDeTramo`**.
Es dramático a propósito. Marcado para playtest.

### 3.5 Curvas de progresión
`AnimationCurve` inicializada exactamente con la recta del lerp de la spec
(`ancho 1.45→0.82`, `velocidad 1.95→3.05` sobre `converts/14`).
El default es el número tuneado; doblar la curva reemplaza el lerp.

### 3.6 Animaciones — fps por prefijo, frames autodetectados
El escáner detecta cuántos frames hay realmente. Los fps salen de un diccionario
configurable por prefijo, no hardcodeados.

| Animación | Frames reales | FPS | Modo |
|---|---|---|---|
| `walk` | 6 | 12 | loop |
| `espera` | 4 | 8 | loop |
| `timbre` | 3 | 12 | one-shot |
| `exito` | 3 | 12 | one-shot |
| `vecino_si` / `vecino_no` | 2 | 8 | one-shot |
| `fx_ondas_timbre` | 4 | 12 | loop |
| `idle` | 1 | — | estático |

`chr_predicador_idle` (1 frame, pose neutra para menús) y `chr_predicador_espera`
(4 frames en loop, se acomoda la corbata) son animaciones **distintas**.

### 3.7 Audio — loops del diseñador, no síntesis
El diseñador entrega 4 loops sobre Am–F–C–G (bajo, pad, arpegio, hi-hat).
La fase 10 construye mezclador por capas + filtro pasabajos + mapeo a hora del día
contra `AudioClip` vacíos. **La fase no depende de que los archivos existan.**

### 3.7b Paleta — 29 entradas, 28 en uso, más un color exento
La paleta del proyecto tiene **29 entradas**; el barrido de los sprites encuentra **28**.

**`#FF9A5B`** (naranja cálido intermedio) es **color reservado sin uso actual**. Se
esperaba que apareciera en los finales y no apareció. **No se saca de la paleta**: si se
borra, cualquier asset futuro que lo use se cuantizaría a un color equivocado.

**`#FFF6E6` NO va en la paleta.** Lo usan `cine_haz_luz`, `cine_rayos_radiales` y
`cine_destello_00..03`, que van con **blending aditivo** y no participan del mundo.
⚠️ **Nunca cuantizar esos seis sprites**: conservan degradado de alpha y pasarlos por el
ajuste de paleta los deja en bandas duras.

⚠️ **`fx_ventana_encendida` tampoco se cuantiza**, por la misma razón y con una vuelta más:
su RGB es **una sola entrada de paleta** (`#FFE6B0`, la misma del gradiente de luz de
`DayCycleConfig`) y lo único que varía es el **alpha**. Un cuantizador que mire RGB no
encuentra nada que corregir; uno que aplane el alpha lo destruye. Son siete sprites exentos.

### 3.8 Cámara — conflicto de la spec, resuelto
La spec pedía "Upscale Render Texture **y** Pixel Snapping activados".
**En URP 17 eso no es expresable:** ambos son estados mutuamente excluyentes de un
único enum `PixelPerfectCamera.GridSnapping` (`None` / `PixelSnapping` /
`UpscaleRenderTexture`), y las propiedades viejas `upscaleRT` y `pixelSnapping`
están `[Obsolete]` desde 2021.2 — cada setter pisa al otro.

**Resuelto:** `GridSnapping.UpscaleRenderTexture`. Renderiza a una RT del tamaño de
referencia, con lo cual el movimiento subpíxel ya es imposible: subsume lo que hacía
Pixel Snapping.

Además se fuerza `m_FilterMode = Point` (el default de Unity 6 es `RetroAA`, que mete
un filtrado bilinear en el upscale final y ablanda el pixel art).

#### 🚨 `orthographicSize` NO vale 3.375 en runtime. Lo cambia la Pixel Perfect Camera.

**Esto vale para TODO elemento anclado a un borde de pantalla, no solo para la barra de
tiempo.** Es de los pozos que no se notan hasta que alguien juega en otra resolución.

El diseño dice orthographic size **3.375** (216 px ÷ 2 ÷ 32 PPU) y eso es lo que está en el
`.unity`. Pero la Pixel Perfect Camera **reescribe `orthographicSize` en runtime** para que
el upscale caiga en un múltiplo entero de la ventana real. Medido en Play Mode: **3.688**.
Cuanto más se aleje la ventana de la relación de referencia, más se aleja el número.

Consecuencia: **el borde superior de la pantalla no está donde dice el diseño.** Un elemento
posicionado con un offset fijo horneado en el prefab se despega del borde —o se va afuera—
apenas la ventana cambia. Y en el Editor no se ve, porque el Game View suele estar cerca de
la relación de referencia.

**Regla: la posición de cualquier UI pegada a un borde se calcula cada cuadro desde
`targetCamera.orthographicSize`, nunca se deja fija.** Es lo que hace `TimeBarView`:

```csharp
float y = targetCamera.orthographicSize - ProjectConstants.ToUnits(marginTopPixels + half);
```

El margen se sigue expresando en píxeles en el Inspector —que es como se piensa— y solo la
conversión a unidades depende de la cámara. Con la cámara a 3.688, la barra quedó a los 11 px
del borde que pedía su config, no a los que hubiera dado la cuenta con 3.375.

⚠️ **Y ese número se cuela en los commits.** Guardar la escena escribe el `orthographic size`
que la cámara tenga en ese momento, más un `m_NormalizedViewPortRect` con los bordes
recortados (`x: 0.00097`, `width: 0.997`). Ya pasó una vez: un commit que solo tocaba un `bool`
se llevó puesto `3.375 → 3.6875`. **Revisar `git diff` de `Game.unity` antes de commitear**, y
si aparecen esas líneas, revertirlas a mano y recargar la escena desde disco con
`EditorSceneManager.OpenScene(..., OpenSceneMode.Single)` para que memoria y disco coincidan.

⚠️ El mismo cuidado con `Camera.aspect`: al muestrear la pantalla para verificar, la relación
de la cámara **no** es la de la RenderTexture que uno crea. Eso ya rompió una medición del
contorno del aro (§11.8) convirtiendo un círculo en elipse.

### 3.9 Cielo — es el clear color, y la luz global NO lo afecta
El skyline es una silueta plana de un solo color (`#3A3050`) sobre transparente, así que
el cielo **es** el `backgroundColor` de la cámara. Hoy: `#A8B4C6`, mediodía.

⚠️ **La Global Light 2D no afecta al clear color.** En la fase 7 la transición de cielo
tiene que salir del `Gradient` de `DayCycleConfig` escribiendo **directo sobre
`camera.backgroundColor`**, no de la luz. Si se intenta atardecer solo con la luz, el
cielo se queda celeste de mediodía mientras todo lo demás oscurece.

Si alguna vez hace falta degradado vertical en vez de color plano, la salida es un quad
detrás de todo con shader de gradiente, **no** un sprite. No hacerlo por ahora: el color
plano es más fiel a la referencia y más barato.

### 3.9b Ventanas 50×50 y el tell de cortina
Las tres ventanas se reemplazaron: **32×30 → 50×50** (mismo GUID, no se rompió nada).
El vidrio pasó de 20×18 a 42×38, que es lo que la cortina necesita.

**Anclaje sobre la pared de 96×64:** la ventana va a **4 px del borde superior de la
pared**, no centrada, para que quede alféizar visible abajo.
→ ocupa Y `10..60 px` = `0.3125 .. 1.875` unidades, con pivot BottomLeft.

**Calce de la cortina:** offset **+4 x, +2 y** respecto del origen de la ventana.
⚠️ La cortina importa con pivot **Center** (`fx_` cae en el default de
`SpriteImportSettings`, que no se toca) mientras la ventana es **BottomLeft**. El offset
se expresa en `HouseGenConfig` en píxeles desde el origen de la ventana —como lo piensa
el artista— y la conversión al pivot Center se hace al componer.

#### Regla: la cortina va en una ventana dedicada
La cortina cerrada tapa el vidrio entero. Si fuera en las dos ventanas nunca se vería el
parpadeo de `env_ventana_tv` (+0.35, una de las señales de más peso). Y si apareciera
recién al tocar el timbre, su sola presencia delataría que hay alguien.

```
WindowA = ventana de SEÑAL
          normal / persiana / tv según el generador. Nunca lleva cortina.

WindowB = ventana de TELL
          siempre env_ventana_normal + cortina en frame 00, en TODAS las casas,
          ocupadas o no. Idéntica en todas, así que no delata nada.
          El tell es el MOVIMIENTO a frame 01, no la presencia de cortina.
```

**WindowB va del lado de la puerta más cercano al jugador cuando toca el timbre**, para
que el movimiento entre en el mismo golpe de vista que la puerta.

#### Timing del tell (2 frames: asomo discreto, no deslizamiento)
- Al **55%** de la espera: frame 01 durante **0.4 s**, vuelve a frame 00.
- Se repite cada **1.2 s** mientras siga la espera. La repetición es a propósito: si el
  jugador estaba mirando la puerta y se perdió el primer asomo, tiene otro.
- Los pasos acelerando siguen como respaldo sonoro.

⚠️ **Con la espera mínima el asomo NO se repite.** Medido sobre el reloj real: con 1.8 s
el primer asomo cae en 0.99 s y el segundo caería en 2.19 s, o sea después del final. La
repetición como red de seguridad **solo existe en las esperas largas** (con 2.8 s hay dos:
1.55 s y 2.75 s). No es un bug — sale de combinar 0.55 con un piso de 1.8 s y un intervalo
de 1.2 s — pero conviene tenerlo presente al tunear: subir la fracción del tell acorta
todavía más la ventana de repetición.

Pasos medidos con espera de 2.8 s: intervalos 0.35, 0.30, 0.27, 0.22 — aceleran como
corresponde. No llegan a 0.17 porque el último paso cae antes del final.

Reparto de responsabilidades: la **duración del asomo (0.4 s) vive en el clip**
`Cortina_Asomo` y se reajusta desde la ventana de Animation. La **fracción de disparo
(0.55) y el intervalo de repetición (1.2 s) viven en `WaitConfig`**, porque son lógica de
espera y no de animación.

### 3.10 Señales — regla general de composición
> **Ninguna señal se dibuja fuera del footprint de su casa.**

Las señales son el sistema de lectura del juego. Si una vive en una banda que otra capa
puede tapar, el bug vuelve con cualquier cambio de composición futuro. El jugador nunca
tiene que mirar afuera de la casa para leerla.

Consecuencias, decididas ya (reemplazan la pregunta abierta 10.1):
- **El auto se estaciona en la entrada, no en la calle.** Va dentro del terreno, entre la
  reja y la casa, al costado del camino de la puerta, a nivel de la casa y no sobre el
  asfalto. Además es más fiel al conurbano.
- **Los arbustos de frente pasan a ser props sueltos con huecos**, no una tira continua.

Los dos cambios van juntos: el auto adentro resuelve la lectura, los arbustos sueltos
resuelven que la calle no desaparezca.

#### 3.10b Modos de montaje — cuatro señales no son "un sprite en un ancla"
El primer modelo daba por hecho que toda señal era un sprite pegado a un ancla. Es falso
para cuatro de las siete, y arrancar la fase 4 así habría metido casos especiales dentro
de `HouseInstance`, que es justo lo que el diseño evita.

| Modo | Señales | Cómo se compone |
|---|---|---|
| `PropEnAnclaDePared` | buzón, ropa tendida | sprite colgado de la pared |
| `PropEnAnclaDeSuelo` | auto, farol de entrada | sprite apoyado en el terreno |
| `FranjaTileada` | pasto crecido | 96×24 con draw mode Tiled al ancho del terreno |
| `VarianteDeVentana` | persianas, TV | pisa el sprite de WindowA |

Más dos campos opcionales: `additiveLayer` (`fx_farol_luz`, `fx_ventana_luz`) y
`additiveLayerAnimation` (el parpadeo del TV).

- **El parpadeo del TV no cambia sprites**: `env_ventana_tv` es un sprite único. Sale de
  animar el **alpha de la capa aditiva**. Al ser curva float lleva keyframe de cierre
  (13 keys para 12 valores) — la regla opuesta a las curvas de sprite.
- `fx_ventana_luz` mide 48×46 y fue diseñado para la ventana vieja de 32×30. Va a
  **tamaño nativo centrado sobre el vidrio**; si no rinde en pantalla, escalar **×2
  exacto**, nunca en fracciones.
- **Material aditivo**: `Assets/Materials/SpriteAdditive.mat`, sobre un shader propio
  (`Assets/Shaders/SpriteAdditive.shader`). Hizo falta escribirlo porque el
  Sprite-Unlit-Default de URP no expone el estado de blending y las luces necesitan
  `Blend One One`. Está en HLSL contra `Core.hlsl` para que sea URP nativo. Lo va a
  reusar la fase 9 para los seis sprites de luz de las cinemáticas.

#### 3.10c El auto: mínimo 216 px, y cómo no sesgar las probabilidades
El auto mide 80 px y el camino de la puerta ocupa 44 centrados, así que el lado libre es
`ancho/2 − 22`. Para que entren 80 px más 6 de margen: `ancho ≥ 216 px`.
→ **`minimumLotWidthPixels` = 216**, y el auto va siempre del lado más ancho.
Con terrenos uniformes de 190–270, aparece en el **67%** de las casas.

⚠️ **Regla de selección que evita el sesgo** (implementar en fase 4): si en un terreno
angosto el generador quería una señal positiva y el auto no entra, **no dejar el slot
vacío** — eso volvería más negativas de lo diseñado a todas las casas angostas.

```
1. Elegir primero el SIGNO (positiva o negativa), por peso.
2. Elegir una señal concreta COMPATIBLE con el terreno.
3. Si la elegida no entra, sortear otra del mismo signo.
4. Solo si ninguna del signo entra, queda vacío.
```

**Verificación obligatoria en fase 4:** la media de `chance` de las casas de 190–215 px
tiene que dar igual que la de 216–270 px. Si no da, hay un sesgo escondido.

#### ✅ Resultado de la verificación (60.000 casas por modo)
| | angostos | anchos | dif |
|---|---|---|---|
| chance media (Uniforme) | 0.4413 | 0.4382 | **+0.0031** |
| con ≥1 positiva (Uniforme) | 0.4178 | 0.4182 | **−0.0004** |
| chance media (PorPesoRelativo) | 0.4448 | 0.4401 | +0.0048 |
| con ≥1 positiva (PorPesoRelativo) | 0.4182 | 0.4186 | −0.0003 |

**Sin sesgo material, y el modo `Uniforme` queda mejor que el ponderado** — así que se
deja como default. La diferencia residual es *positiva* (los angostos dan un pelo más
alto, no más bajo): al excluir el auto (+0.25, por debajo de la media de las positivas),
las positivas que quedan valen un poco más. Es el efecto contrario al que se temía.

La proporción de casas con al menos una positiva empata en ambos modos (−0.0004), que
era la comprobación fina que podía delatar una distribución corrida detrás de medias que
empatan por casualidad.

#### ⚠️ Métrica de rachas: cuidado con cómo se cuenta
La peor racha de vacías dio **4** con una métrica y **3** con otra:
- **Mal:** contar legibles-vacías reseteando solo con una legible ocupada. Da 4, pero ese
  4 son *tres decepciones con un éxito en el medio* — una casa que pintaba muerta y abrió.
- **Bien:** resetear con **cualquier** casa que abra. Da **3**, que es el objetivo de diseño.

Desde el jugador, cualquier puerta que se abre corta la racha de frustración. La ventana
de verificación usa la segunda.

### 3.11 Capas de decorado — cada cosa a su plano, y la aritmética que lo obligó

Cierra los puntos 1 y 3 del repaso de capturas. Dos de las tres hipótesis del reporte
apuntaban al lugar equivocado, así que primero lo que **no** era:

> **`ParallaxLayer` nunca tocó el eje Y.** Escribe `originPosition.y` literal y solo
> modifica la X. El árbol sobre el techo y el poste flotando eran **oclusión**: los props
> de la capa lejana se apoyaban en la misma línea de caminata que las casas, y la pared
> les tapaba los dos tercios de abajo.

#### Poste de luz → parallax 1.0, y solo en los huecos
Es una **fuente de luz** para la fase 7: tiene que iluminar la vereda por la que camina el
predicador, y una luz que vive en otro plano de profundidad no ilumina nada.

Solo en los huecos entre terrenos, nunca frente a una fachada — misma regla que §3.10.
Un poste delante de un buzón desbordado rompe el sistema de lectura.

Medido: 40×160, opaco hasta la fila 150, borde superior del cuadro en 152 → **entra sin
recortar el farol**. Ancho 40 px contra el hueco más angosto de 85 → entra siempre.
Sobre 4000 casas: **0 postes invaden un terreno**, margen mínimo a la fachada **22.6 px**,
densidad 34.1% de los huecos (≈0.7 postes por pantalla). La densidad es `chancePerGap`.

#### Árbol → también a parallax 1.0 y también en los huecos
Se probó primero como árbol de fondo a 0.55 y **no cierra por dos razones**.

La de números: el sprite mide **120 px** y los techos llegan a **112**. Apoyado, la copa
asoma 8 px y se lee como un bulto sobre el techo — eso era el bug. Para despegarla 16 px
hay que levantar el sprite 66, y entonces el tronco flota.

La de fondo, que es la que manda:

> **A parallax distinto, la alineación relativa con las casas cambia todo el tiempo.** No
> se puede garantizar que un árbol de fondo quede detrás de una casa: inevitablemente
> deriva sobre un hueco, y sobre un hueco una copa recortada levita. **Un prop con base
> definida no puede vivir en una capa que se desplaza respecto del mundo.**

Y el sprite delata el problema: se llama `env_arbol_**vereda**`, tiene cazuela de tierra
cuadrada en la base y mide lo mismo que una casa. Es mobiliario de vereda, no arte de
fondo. Forzarlo a una capa de profundidad es pedirle algo para lo que no fue dibujado.

Resuelto: **el árbol va a parallax 1.0, en los huecos, con el mismo tratamiento que el
poste.** Sin recorte, sin tinte, apoyado en la línea de caminata. Asoma 8 px sobre los
techos vecinos, pero desde adelante, que es como se ve un árbol de vereda de verdad.

#### Un solo decorado por hueco, elegido en una sola tirada
`GapPropPlacer` escucha `HouseSpawner.GapOpened` y reparte. Cada `SceneryPropSet` expone
su propia `chancePerGap`, y **se acumulan**: una tirada por hueco recorre la lista sumando,
y si pasa de largo el total el hueco queda pelado.

Se eligió así, y no con pesos relativos, para que cada número se lea solo en el Inspector
sin tener que dividirlo mentalmente por el total de la lista. ⚠️ La contra: **la suma no
puede pasar de 1**, o los últimos candidatos no salen nunca. `GapPropPlacer` avisa por
consola si eso pasa.

#### El ancho mínimo de hueco es del decorado, no de la cuadra
El árbol mide 72 px y entraría en el hueco más angosto de 85, pero ahí queda encajado
tocando las dos rejas (6.5 px por lado). Las dos salidas obvias eran malas: subir
`gapMinPixels` toca el ritmo de la cuadra, que ya está validado y es lo único de ahí que
afecta al juego; y bajarle la chance al árbol lo hace más raro en TODOS lados para
arreglar algo que solo pasa en los angostos.

**La perilla correcta es `minimumGapPixels` en el propio `SceneryPropSet`** — el mismo
patrón que `minimumLotWidthPixels` del auto. El árbol pide **100 px**: le quedan 14 px por
lado y sigue entrando en el 83% de los huecos.

| | sprite | pide | chance | reparto real (6000 huecos) | margen mínimo |
|---|---|---|---|---|---|
| Poste | 40 px | 40 px | 35% | **40.1%** | 22.5 px |
| Árbol | 72 px | 100 px | 30% | **25.4%** | 14.0 px |
| vacío | | | 35% | **34.6%** | |

0 decorados invaden un terreno, 0 árboles en hueco angosto. El poste sube de 35 a 40.1%
porque hereda la parte del árbol en el 16.9% de huecos donde el árbol no entra.

#### Regla general: la sustitución se prohíbe donde hay pesos, se permite donde hay decoración

Cuando el elegido no entra, `GapPropChooser` **reparte su parte entre los que sí entran**,
en proporción a las chances de cada uno. Es lo contrario de la regla §3.10c para las
señales de una casa, y la diferencia importa:

- **Señales: no se sustituye.** Tienen peso de juego. Si el auto no entra y se lo
  reemplaza por otra cosa, las casas angostas se vuelven más negativas de lo diseñado y se
  corre el balance sin que nadie lo note.
- **Decorado: se sustituye.** Un poste en lugar de un árbol no cambia ninguna
  probabilidad, ninguna lectura ni ninguna decisión del jugador. Y queda mejor: huecos
  angostos con poste y anchos con árbol es como se reparte el mobiliario de una cuadra de
  verdad.

Sustituir mantiene además la proporción de huecos pelados **igual en toda la cuadra**
(34.6% medido contra 35% configurado), que es lo que se ajusta en el Inspector. Vaciar en
vez de sustituir dejaría los tramos de huecos angostos visiblemente más pelados.

`GapPropChooser` vive en `Simulation` y es una clase plana: elegir es función del ancho y
de las probabilidades, así que el reparto se verifica sobre 6000 huecos sin entrar en Play
Mode. El componente solo planta lo que el chooser decide.

#### Arbustos → matas sueltas, y solo sobre el asfalto
Medido antes de tocar: **nunca estaban invadiendo la vereda**. La tira ocupaba exactamente
la banda de asfalto. Lo que cambió es que dejaron de ser tira.

✅ **Llegaron `prop_arbusto_a/b/c`**: tres matas de 48×36 con pivot BottomCenter y
**ninguna toca sus bordes laterales**, así que son props de verdad. Con esto desaparece el
corte plano de ~8 px que dejaba partir `bg_arbustos_frente`, que estaba dibujado como tira.

Apoyadas en el borde inferior del cuadro (−64 px) llegan como mucho a −28, y la vereda
arranca en −24: **no la tocan**. Eso importa porque están a parallax 1.25, o sea delante
del jugador; si subieran, taparían al predicador y a la fila de seguidores justo cuando
hay que verlos.

`SceneryPropSet` pasó a tener una **lista de variantes** en vez de un sprite: se sortea una
por mata plantada. Sumar una mata nueva es arrastrar el sprite al asset, sin código.
Medido en Play: 6 matas en pantalla, las 3 variantes presentes, huecos de 71 a 145 px
contra los 50–160 configurados.

⚠️ **Cuidado con los pivots al sembrar.** Las matas importan BottomCenter (prefijo
`prop_`) y las tiras BottomLeft (`bg_`, `env_`). `ScenerySpawner` y `GapPropPlacer` tratan
su `nextX` como el BORDE IZQUIERDO y corrigen con `pivot.x / rect.width`; sin eso, media
capa aparece corrida media mata. Es el mismo bug que ya había aparecido con el pasto.

#### La capa 0.55 se llena con una TIRA, no con props
El relleno correcto de esa capa **no es un árbol suelto sino una tira de silueta
tileable** —copas y techos de la cuadra de atrás en un tono plano, como `bg_skyline_lejano`
pero más cerca. Una tira sí tolera derivar sobre los huecos, porque es continua y no tiene
base. Ese es exactamente el motivo por el que el árbol se fue a parallax 1.

✅ **Llegó como `bg_arboles_lejanos`**: 384×72, un solo color `#3A3050`, pivot BottomLeft,
y tilea sin costura (0 filas donde el borde izquierdo y el derecho no coinciden).

Apoya en `SkylineBottomY` (64 px), igual que el skyline, así las dos bandas nacen desde
detrás de los techos. Ocupa 64..137 px y los techos llegan a 112, o sea que asoman **25 px
de silueta** sobre la línea de techos.

⚠️ Comparte el `#3A3050` del skyline, así que donde se superponen se leen como una sola
masa; lo que separa los dos planos es el contorno moviéndose a distinta velocidad. Es la
dirección de arte pedida, pero conviene mirarlo con el juego andando.

#### Cómo se agrega un decorado nuevo
Crear un asset `SceneryPropSet` y colgar un `ScenerySpawner` de la capa que corresponda.
**Sin código.** Los tunables (recorte, altura, tinte, separaciones, probabilidad por
hueco) viven en el asset y no en la escena, porque `GameSceneBuilder` reconstruye la
escena entera y se llevaría puesto cualquier número puesto a mano en un componente.

---

## 4. Estado de las fases

| # | Fase | Estado |
|---|---|---|
| 1 | Escena base y cámara pixel-perfect | ✅ hecha |
| 2 | Animaciones (clips + controllers como assets) | ✅ hecha |
| 3 | Datos (ScriptableObjects + custom editors) | ✅ hecha |
| 4 | Mundo (casa componible, generación, pool, parallax) | ✅ hecha |
| 5 | FSM, caminar, timbre, espera, pity + verificador | ✅ hecha |
| 6 | Skillcheck | ✅ hecha y **vista andando** (§11.6) |
| 7 | Tiempo, luz, comitiva | ✅ hecha y completa |
| 8 | UI e input (incluye arcade USB) | ✅ hecha — sin contador, por decisión (§13.6) |
| 9 | Estado de partida, selección de religión y finales | ✅ hecha — capa de estado, fuente, selección y los tres finales (§14) |
| 10 | Audio por capas | ⬜ **bloqueada** — los 4 loops todavía no existen |
| 11 | Pulido y verificación de los 10 criterios | ⬜ **en espera del playtest**, a propósito |

Un commit por fase.

### 🛑 Las dos que faltan siguen frenadas, y no por olvido

El playtest ya pasó y de ahí salió la tanda de §15 (felpudo, insistir y correcciones), que va
ANTES que estas dos.

**La 10 espera los loops.** §3.7 dice que la fase se puede construir contra `AudioClip` vacíos
y eso sigue siendo cierto técnicamente, pero el diseñador está haciendo los sonidos él y decidió
no arrancarla hasta que los cuatro loops Am–F–C–G existan. Sin material, la mezcla por capas se
afina a ciegas.

**La 11 es lo último.** Pulir antes de que la tanda de §15 esté cerrada sería pulir dos veces,
por el mismo motivo por el que se esperó al playtest.

⚠️ **No arrancar ninguna de las dos sin que el diseñador avise.**

---

## 5. Input (fase 8) — requisito crítico

> ✅ **El provisional se BORRÓ, no se envolvió.** `ProvisionalOneButtonInput.cs` ya no
> existe en el repo ni en las assemblies compiladas; se verificó por reflexión que el tipo
> no resuelve. Lo reemplaza `OneButtonInput`.

**Cualquier botón de cualquier dispositivo dispara la única acción del juego.**
No hay mapeo porque no hay más de una acción.

Debe andar con: barra espaciadora, Enter, click, tap, cualquier botón de gamepad
estándar, y **un botón físico de arcade por USB**.

⚠️ Los encoders USB de arcade **no siempre se presentan como `Gamepad`**. Muchos
aparecen como `Joystick` genérico o HID crudo. Atar los bindings solo a
`<Gamepad>/buttonSouth` hace que el control no dispare nada aunque Windows lo detecte
perfecto — bug caro de diagnosticar porque el control "anda" en el panel de control.

Cubrir `Gamepad`, `Joystick` y HID genérico. Si los bindings estáticos no alcanzan,
usar `InputSystem.onAnyButtonPress` y filtrar lo que no sea de juego.

Dos gestos, **ambos deben andar con el arcade**:
- tap corto → acción principal
- mantenido 0.6s → confirmar en selección de religión

Entregable extra: **ventana de Editor de diagnóstico** que muestre en vivo qué
dispositivos hay conectados y qué control se está apretando. Sirve para distinguir en
dos segundos si el problema es detección o binding cuando se conecte el gabinete.

---

## 5b. Constantes de simulación — que todas las corridas usen las mismas

`SkillcheckConfig` **no tiene campo de duración**: el tiempo de un skillcheck sale de que
la aguja recorra `zoneAhead` hasta la zona. Para que dos simulaciones distintas den
números comparables, se fija acá de dónde sale cada valor estimado.

| valor | cuánto | de dónde sale |
|---|---|---|
| velocidad de aguja base | **1.95 rad/s** | curva de §3.5 a 0 conversiones (va a 3.05 a 14) |
| recorrido hasta la zona | 1.25–3.1 rad | `zoneAheadMin/Max` de `SkillcheckConfig` |
| duración de un skillcheck | `zoneAhead / 1.95 + 0.30` ≈ **1.4 s** | lo de arriba + `chainPauseSeconds` |
| acierto del skillcheck | **0.75 fijo** | para aislar la lectura de señales de la habilidad de timing |
| aborto de la espera | **1.6 s** | el tell más tardío cae en 1.54 s (0.55 × 2.8), así que a 1.6 ya se vio |

⚠️ **La duración real sube con la comitiva.** La cadena de objeciones crece con los
seguidores (§3.2: 1 eslabón hasta 3 seguidores, 4 a partir de 12), y cada eslabón suma otro
recorrido de aguja más `chainPauseSeconds`. Hoy no importa porque afecta parejo a las
cuatro estrategias comparadas, **pero cuando se simule el día completo con seguidores hay
que multiplicar la duración por la cantidad de eslabones.**

---

## 6. Tests

Unitarios sobre las clases planas y deterministas, que es donde un cambio silencioso
arruinaría el balance. No se testea presentación ni UI.

| clase | estado |
|---|---|
| `PityTracker` | ✅ 8 tests |
| `WaitTimeline` | ✅ 6 tests |
| `HouseOccupancyResolver` | ✅ 5 tests |
| `SkillcheckDifficulty` | ✅ 9 tests |
| `SkillcheckAttempt` | ✅ 8 tests |
| `SkillcheckSession` | ✅ 8 tests |
| `DayClock` | ✅ 11 tests |
| `RunStateMachine` | ✅ 10 tests |
| `ReligionCarousel` | ✅ 6 tests |
| `HouseLayoutGenerator` | ⬜ pendiente |

**71 pasan, 0 fallan.**

Hizo falta crear las tres assembly definitions del proyecto (`BuenosDias.Runtime`,
`BuenosDias.Editor`, `BuenosDias.Tests`): sin ellas todo compilaba en `Assembly-CSharp`,
que el Test Framework no puede referenciar. ⚠️ `Light2D` vive en
`Unity.RenderPipelines.Universal.**2D**.Runtime`, que es una assembly aparte de la de URP.

### Dos reglas que salieron de escribirlos
- **Fijar el valor del azar, no la semilla.** `FixedRandom` reemplaza a
  `new System.Random(seed)`. Una semilla elegida para que "nunca abra" deja de servir
  apenas el pity sube, porque cambia la probabilidad contra la que se compara. Dos tests
  pasaron en verde por casualidad hasta que el pity acumuló.
- **Un test tiene que aislar UN mecanismo.** El test del techo del pity medía en realidad
  la garantía dura, que disparaba en la cuarta vacía y reseteaba el pity. Se corre
  `guaranteedAfterEmptyRun` fuera de alcance para que no se crucen.

Los configs de los tests se arman en memoria con `ConfigFactory`, nunca desde los assets:
si un test dependiera del balance tuneado, cambiar un número en el Inspector rompería
tests que no tienen nada que ver, y el diseñador aprendería a ignorar el rojo.

### 6b. Verificar con el Editor abierto en otro proyecto

El puente MCP se conecta a **una** instancia de Unity, que puede no ser la de este
proyecto. Cuando pasa, no hay Test Runner ni consola: `Application.dataPath` lo dice en
una línea y conviene chequearlo antes de creerle a un "0 errores".

La salida es `dotnet` con un **shim de UnityEngine**: unas 200 líneas con solo firmas
(`Mathf`, `ScriptableObject`, `MonoBehaviour`, `SpriteRenderer`, `Texture2D`…) contra las
que compilan los `.cs` REALES del proyecto. Da dos cosas:

- **Compilación** de `Config` + `Simulation` + `Core` + `Gameplay` + `Presentation`. No
  cubre `Assets/Editor` (necesita `UnityEditor`).
- **Ejecución** de los cuerpos de los tests puros, que no tocan Unity.

⚠️ **No reemplaza al Test Runner.** El shim puede diferir de la API real en los dos
sentidos: puede rechazar algo válido (le faltaba el `Color → Color32` implícito, que Unity
sí define) y puede aceptar una firma que Unity no tiene. Sirve para no entregar código sin
compilar; **lo que toca el Editor o la escena queda sin verificar hasta abrir el proyecto.**

---

## 7. Assets faltantes (los provee el diseñador)

| Asset | Impacto | Estado |
|---|---|---|
| Cortina moviéndose (2 frames) | **Alto** — es el tell principal del juego | ✅ **llegó** |
| Crucifixión (loma, cruz, silueta, maletín, 3 cuervos) | Final de 0 conversiones | ✅ **llegó** |
| Ascensión (nubes ×2, haz, rayos, destello ×4) | Final de 10+ conversiones | ✅ **llegó** |
| 4 loops de música Am–F–C–G | Fase 10 | en producción |
| Tira tileable de silueta para la capa 0.55 | Llenaba la capa vacía | ✅ **llegó** como `bg_arboles_lejanos` |
| Matas sueltas de arbusto | Sacan el corte plano de las puntas | ✅ **llegaron** como `prop_arbusto_a/b/c` |
| Ventana iluminada aditiva, que NO sea señal | **Bloqueaba el encendido de luces de la fase 7** (§12.4) | ✅ **llegó** como `fx_ventana_encendida` |

Total actual: **135 PNG**. El placeholder de cortina de 1 píxel se retiró.
`bg_arbustos_frente` queda sin uso, reemplazado por las tres matas.

**Los dos pedidos eran el mismo problema en direcciones opuestas**: arte dibujado como
tira que se necesitaba como prop (los arbustos), y arte dibujado como prop que se
necesitaba como tira (el árbol en la capa lejana). Los dos entraron sin tocar la
arquitectura: uno como hijo de `Parallax_Far`, el otro como variantes de un
`SceneryPropSet`.

**Cómo disparar el tell desde el juego (fase 5):**
`animator.SetTrigger("Asomar")` sobre el `Cortina.controller` de WindowB. El grafo es
reposo + golpe: estado `Quieta` por defecto y `Asomo` disparado por trigger, que vuelve
solo al terminar. Se usa Trigger y no un entero porque las transiciones no pueden ir a sí
mismas: con un int habría que bajar el valor y volver a subirlo desde el juego para
repetir el asomo. El Trigger se consume solo.

**Los 15 sprites de cinemáticas** están importados en `Assets/Sprites/Cinematics` con
pivot Center. Los seis de luz necesitan **material aditivo** — se resuelve en la fase 9.

---

## 8. Deuda técnica anotada

- **`PaletteSwapper` por CPU.** Genera una `Texture2D` por sprite por paleta.
  Decisión: se mantiene, con precalentado del cache al elegir religión y limpieza al
  cambiarla. Alcanza para 6 personajes y una religión por partida.
  *Si algún día hay muchos más seguidores en pantalla, migrar a shader con textura de
  lookup de los 29 colores: más rápido, sin `Read/Write`, sin basura.*
- **Día de 72s vs 14 conversiones para dificultad máxima.** Apretado: exige ~5s por
  casa. Puede ser deliberado. A playtest, con modo debug de tiempo infinito.

---

## 9. Reglas de código

- Una clase, una responsabilidad. **Máximo 200 líneas DE CÓDIGO** — no físicas. La regla
  existe para prevenir clases-dios; los comentarios, los tooltips y los renglones en blanco
  no hacen una clase-dios. Cumplirla sacando documentación sería el resultado opuesto al que
  busca. Para medir: contar el cuerpo de la clase descontando `///`, `[Header]`, `[Tooltip]`,
  `//` y líneas vacías.
- Nada de números mágicos: todo valor ajustable va a un SO o a un `[SerializeField]`
  con `[Tooltip]` que indique la unidad.
- Nada de `GameObject.Find` ni `FindObjectOfType` en runtime.
- Nada de singletons públicos mutables.
- La lógica no dibuja y la vista no decide.
- Comunicación por eventos; la UI escucha al juego, el juego no conoce la UI.
- `readonly`, `private set`, campos privados con `[SerializeField]`.
- Comentarios en español, sobre el **por qué**. XML docs en todo lo público.
- **No se toca `Assets/Editor/SpriteImportSettings.cs`.**

### Gotchas de Unity 6.5 encontrados en el camino
- `Object.GetInstanceID()` está **obsoleto y compila como error** (CS0619). Usar
  `GetEntityId()`. Ya estaba anotado en `PaletteSwapper.cs`.
- `PixelPerfectCamera.upscaleRT` / `.pixelSnapping` están `[Obsolete]`: usar
  `gridSnapping`. Ver §3.8.
- `AnimationPlayMode` **ya existe en UnityEngine**. El enum propio se llama
  `ClipPlayMode` para evitar el CS0104.
- **Largo de clip según el tipo de curva** — el más traicionero:
  - Curvas de **referencia a objeto** (sprites): `length = tiempoDelÚltimoKey + 1/fps`.
    Unity ya le da al último frame su duración completa. **No** agregar un keyframe
    duplicado al final: alarga el clip un frame y el último sprite queda el doble de
    tiempo (en la caminata se siente como un tirón en cada vuelta).
  - Curvas **float** (posición, color): `length = tiempoDelÚltimoKey`. Acá **sí** hace
    falta el keyframe de cierre, y es lo que hace el clip de cortina.
- **No llamar `AssetDatabase.Refresh()` y seguir usando tipos propios en el mismo
  comando MCP**: si hay scripts editados, el domain reload se lleva puesto el assembly
  dinámico del comando en ejecución. Refrescar en un comando y trabajar en el siguiente.
- **`Sprite.Create` para recortar: usar `SpriteMeshType.FullRect`.** El mesh `Tight`
  (el default) necesita leer los píxeles de la textura, y las texturas del proyecto no
  importan como legibles. Recortar con `FullRect` no toca la textura: solo cambia las UV.
- **El Editor no tickea sin foco.** En Play Mode por MCP el mundo queda congelado hasta
  que se pone `Application.runInBackground = true`. Es un `ProjectSettings`: hay que
  **devolverlo a `false`** al terminar, o queda commiteado un cambio que nadie pidió.
- **`Unity_SceneView_Capture2DScene` toma `worldX`/`worldY` como ORIGEN (esquina inferior
  izquierda), no como centro.** Para encuadrar lo mismo que la cámara:
  `worldX = camX − 6`, `worldY = −2`, `12 × 6.75`. Con `pixelsPerUnit` 64 sale ×2 exacto.
- **El orden de `Awake` entre dos GameObjects NO está definido.** Si un componente lee en
  su `Awake` algo que otro crea en el suyo, se guarda un null la mitad de las veces y
  revienta mucho después. Pasó exactamente eso con `PreacherController` leyendo
  `HouseSpawner.Pity`: no fallaba al arrancar, fallaba al tocar el primer timbre.
  **Regla: leer estado de otro componente va en `Start`, no en `Awake`.** Unity sí
  garantiza que todos los `Awake` corren antes que cualquier `Start`.
- 🚨 **El Input System tiene buffers de estado POR TIPO DE UPDATE.** `IsPressed()` leído
  desde un comando del puente devuelve el buffer del Editor, no el que ve el juego: se llega
  a leer "nada apretado" mientras la pantalla muestra el botón apretado. Al verificar input,
  medir el EFECTO en la escena y no el input. Ver §14.6, que tiene las tres trampas.
- 🚨 **`orthographicSize` cambia en runtime con Pixel Perfect Camera** (3.375 de diseño →
  3.688 medido). Toda UI anclada a un borde tiene que recalcular su posición cada cuadro
  desde la cámara, nunca hornear el offset. Ver §3.8, que lo explica entero.
- **Un `ScriptableObject` por archivo, con el nombre de la clase.** Unity solo encuentra
  el script si el `.cs` se llama igual que la clase. Agrupar los 5 canales de evento en
  un `GameEventChannels.cs` creó los assets con el script roto
  (*"No script asset for IntEventChannel"*). Por eso cada canal vive en su propio archivo
  aunque el cuerpo sea de una línea.

---

## 10. Preguntas abiertas

### 10.1 ~~Los arbustos tapan la calle~~ — RESUELTO, ver §3.10 y §3.11
Se cerró con la regla general de composición de señales: el auto se estaciona dentro
del terreno y los arbustos pasan a props sueltos, ya implementados.

### 10.2 ~~Layer `Far` sin spawner~~ — RESUELTO, ver §3.11
Los 6 props puestos a mano de la fase 1 salieron. Poste **y árbol** se fueron a parallax
1.0 con `GapPropPlacer`, y la capa quedó vacía a la espera de `bg_copas_lejanas`.

### 10.4 ⚠️ ABIERTA Y GRAVE — el pity se puede farmear con casas muertas

Simulación de día completo (72 s, Testigos, skillcheck fijo 0.75, 20.000 partidas),
comparando estrategias por **conversiones finales**, no por tasa de acierto:

| estrategia | conversiones | timbrazos | abren |
|---|---|---|---|
| A. toca todo, no aborta | 5.06 | 12.9 | 55.2% |
| B. lee y saltea, no aborta | **5.02** | 10.6 | 66.6% |
| C. lee, saltea y aborta a 1.6 s | 5.42 | 11.4 | 66.8% |
| **D. NO lee, toca todo y aborta** | **5.78** | 14.5 | 55.4% |

**El orden es D > C > A ≈ B. La estrategia óptima es ignorar las señales.**

Leer sin abortar (B) es apenas peor que no leer (A): saltear ahorra la espera de la
casa vacía, pero la caminata se paga igual, así que se cambia cantidad de timbrazos por
tasa de acierto y empata. Eso ya era malo. Lo grave es D.

#### Por qué gana D: tocar casas muertas es combustible barato para el pity
La cuenta marginal no explicaba el resultado —una casa muerta rinde 0.068 conv/s contra
un promedio de 0.080— así que se midió el pity al momento de tocar cada casa legible:

| | legibles tocadas | abren | pity medio al tocar | garantizadas |
|---|---|---|---|---|
| C (saltea las muertas) | 11.4 | 66.8% | 0.080 | 0.3% |
| D (toca las muertas) | 10.1 | **71.6%** | **0.140** | **2.0%** |

D toca **menos** casas legibles pero cada una abre 4.8 puntos más, porque llegó con el
pity cargado. Las casas muertas cuestan 1.6 s con el aborto y se usan como fichas para
comprar mejor suerte en las buenas.

> §3.3 dice que el pity nunca puede hacer que abra una casa que se ve muerta, y eso se
> cumple. Lo que nadie previó es lo inverso: **nada impide usar las casas muertas como
> combustible para cargar el pity de las buenas.** La regla protegía la confianza del
> jugador en lo que ve; de paso creó una moneda.

El aborto es lo que lo hace rentable: sin aborto, la casa muerta cuesta 3.15 s y el farmeo
no paga (A ≈ B). El aborto la baja a 1.6 s y el exploit se abre.

#### ✅ Resuelto: el umbral de legibilidad pasa a ser SIMÉTRICO
Una casa por debajo de 0.42 queda fuera del sistema de compensación **en las dos
direcciones**: ni recibe pity, ni lo alimenta.

La razón no es que fuera la palanca más quirúrgica sino que **era un error conceptual, no
un desbalance**: el pity existe para compensar frustración, y en una casa que se veía
muerta no hay frustración — el jugador leyó bien y decidió tocar igual. Que eso sumara
compensación no tenía sentido en ningún escenario.

Las otras dos palancas se descartaron: encarecer el aborto castiga a C, que es justo la
estrategia que se quiere premiar; y bajar la chance de las muertas haría que las señales
negativas mientan menos, o sea lectura más determinista y juego más plano. **Ese 18% de
sorpresa es lo que hace que valga la pena mirar.**

| estrategia | antes | después |
|---|---|---|
| A. toca todo, no aborta | 5.06 | **4.62** |
| B. lee y saltea, no aborta | 5.02 | 5.02 |
| C. lee, saltea y aborta | 5.42 | **5.42** |
| D. NO lee, toca todo y aborta | 5.78 | **5.35** |

Orden nuevo **C > D > B > A**. El farmeo murió (A cae 8.7%, D cae 7.4%; B y C no se mueven
porque nunca tocaban muertas). Y las dos relaciones que se buscaban están: **leer paga**
(B > A, +8.7%) y **abortar paga** (C > B, +8.0%).

Peor racha de legibles-vacías: sigue en **3**, con 0 rachas de 4 sobre 8000 partidas.

#### ⚠️ Queda abierto: C le gana a D por 1.3%
5.42 contra 5.35. La cuenta marginal explica por qué: tocar una casa muerta rinde
**0.069 conv/s** contra el ritmo de C de **0.0753** — está por debajo del punto de
equilibrio, pero apenas. Saltearlas es correcto por poco.

No se toca nada más hasta que el diseñador decida. La pregunta quedó más chica que antes:
ya no es "¿el sistema de señales sirve?" sino "¿una apuesta al 18% que cuesta 1.6 s
debería valer la pena?".

### 10.3 Composición fina — se juzga cuando se pueda jugar
Acordado con el diseñador: los bugs de composición que queden se revisan en una pasada de
pulido con el juego andando de punta a punta. Juzgar composición desde capturas estáticas
es adivinar. Pendiente concreto: la separación entre casas se midió correcta (86–175 px
sobre 400 casas, 0 techos desbordados), pero la captura que reportaba huecos de ~500 px
era anterior a la última reconstrucción del prefab — reconfirmar con el jugador real.

---

## 11. Fase 6 — el skillcheck

### 11.1 Las cuatro clases y por qué están separadas

| clase | dónde | qué hace |
|---|---|---|
| `SkillcheckDifficulty` | `Simulation` | apila los cuatro multiplicadores y devuelve `SkillcheckSetup` |
| `SkillcheckAttempt` | `Simulation` | **un** eslabón: la aguja gira, se aprieta una vez |
| `SkillcheckSession` | `Simulation` | la cadena: N eslabones separados por `chainPauseSeconds` |
| `SkillcheckRunner` | `Gameplay` | corre la cadena, lleva conversiones y comitiva, avisa por eventos |
| `SkillcheckRing` + `ArcPainter` | `Presentation` | dibujan. No deciden nada |

`SkillcheckSession` tiene **un solo punto de entrada**, `Advance(deltaTime, pressed)`. Con
un `Press()` aparte, el resultado dependería del orden en que lo llamara quien la usa —
justo el bug que aparece cuando alguien reordena un `Update` seis meses después.

**El botón se resuelve ANTES de mover la aguja**, contra el ángulo que se dibujó el cuadro
pasado. El jugador reacciona a lo que vio; moverla primero le cobra un cuadro de latencia
que a 60 fps son 0.03 rad que nadie puede ver ni corregir.

### 11.2 Decisiones que ya están fijadas en tests

- **Fallar un eslabón corta la cadena entera.** La objeción que no se contestó es la que
  cierra la puerta.
- **Dejar pasar la zona es fallar**, no esperar a la vuelta siguiente.
- **Apretar de nuevo sobre una tirada resuelta no hace nada.** Un botón de arcade rebota;
  sin esto, el segundo contacto convierte un perfecto en falla. El destino es un gabinete
  físico, así que no es teórico.
- **El botón durante la pausa entre eslabones se traga.** El aro no está en pantalla y así
  el rebote no se come el eslabón siguiente.
- **La dificultad se congela al abrir la puerta.** La puntería, las conversiones y la
  comitiva de ese momento valen para toda la cadena: convertir en el eslabón 1 no endurece
  el 2.

### 11.3 Geometría del aro — medida sobre los PNG

`ui_skillcheck_aro` y `ui_skillcheck_aguja` son dos lienzos de **128×128** con pivot al
centro, o sea 4×4 unidades a PPU 32. Se apilan en el mismo punto.

| medida | valor | de dónde |
|---|---|---|
| centro del aro | (63.5, 63.5) px | barrido de alpha del PNG |
| círculo interno | radio 45 px, trazo 2 px | fila y=64: opaco en x 18–19 y 108–109 |
| círculo externo | radio 56 px, trazo 2 px | misma fila: opaco en x 7–8 y 119–120 |
| **canal libre entre los dos** | **radio 46 a 55 px** | es donde se pinta la zona |
| largo de la aguja | 28 px desde el pivot | alpha opaco en y 64–91, x 62–65 |

La zona **se pinta en CPU** en una textura de 128 con filtro Point, no con un mesh ni un
shader: un arco vectorial entra con el borde antialiaseado y fuera de la grilla, que es
exactamente lo que rompe la lectura del pixel art. Se repinta **una vez por eslabón**, no
por cuadro — la zona no se mueve, se mueve la aguja.

**Convenio de ángulo, el mismo en lógica y en dibujo:** cero apuntando ARRIBA, creciendo en
sentido horario. En el pintor sale de `Atan2(dx, dy)` (y no `Atan2(dy, dx)`, que daría el
convenio matemático, o sea el otro sentido). En la aguja sale de rotar en **Z negativa**,
porque Unity gira antihorario. El ángulo de `SkillcheckAttempt` **no envuelve nunca**: el
módulo lo hace quien dibuja.

Colores desde la paleta: zona buena `#6F8A5E`, zona perfecta `#FF9A5B`, falla `#A8455A`.
⚠️ El `#FF9A5B` era el **color reservado sin uso** de §3.7b. Ahora tiene uso.

#### ⚠️ Abierto: la aguja no llega a la zona
La aguja mide 28 px desde el centro y el canal donde se pinta la zona empieza en 46. Quedan
**18 px de aire** entre la punta y lo que hay que apuntar. Se lee como un reloj —la aguja
señala el sector— pero no se toca con él. Tres salidas, ninguna aplicada:

1. **Aguja más larga** (arte): que llegue a ~56 px. Es la que respeta el diseño del aro.
2. **Zona más adentro**: `trackInnerRadius`/`trackOuterRadius` son campos del Inspector;
   bajarlos a 18–27 la pega a la punta, pero deja el canal del aro vacío.
3. **Dejarlo así** si al jugarlo se lee bien.

No se escala el sprite: ×0.5 o ×1.5 en pixel art rompe la grilla, y ×2 no entra en pantalla.

### 11.4 Conversión y comitiva — decisión de diseño tomada acá
`SkillcheckRunner` es el dueño de las dos cuentas de la partida: **convertir suma un
seguidor, fallar aplica `FollowerConfig.ApplyPenalty`** (§3.4, `BajarDeTramo`).

Se implementó ahora y no en la fase 7 porque sin eso la comitiva sería siempre 0, la cadena
siempre de **un** eslabón, y toda la mecánica de objeciones encadenadas quedaría escrita y
nunca ejecutada. **Consecuencia jugable: a la cuarta conversión la cadena pasa a 2
eslabones** y la puerta empieza a costar el doble de tiempo. La fase 7 solo dibuja la fila:
se suscribe a `FollowersChanged`.

El castigo de tiempo se calcula con la comitiva que **había** al fallar, no con la que
queda: el recargo por multitud se cobra por el papelón, y el papelón ya ocurrió cuando los
que se van todavía estaban ahí.

`SkillcheckResult` ya lleva `TimeDelta` calculado (`GameConfig.TimeBonusFor` /
`FollowerConfig.TimePenalty`). **Nadie lo consume todavía**: el reloj del día es fase 7.

### 11.5 Basura que dejó el propio tooling
`AssetPathUtility.EnsureFolder` preguntaba por la carpeta con `AssetDatabase.IsValidFolder`.
Dentro de un `StartAssetEditing` el importador está en pausa y eso devuelve `false` para una
carpeta recién creada; como `AssetDatabase.CreateFolder` ante un nombre tomado **no falla
sino que inventa uno libre**, cada corrida del builder de animaciones dejaba una carpeta
vacía más. Se habían commiteado **55** (`Animations 1..29`, `Vecinos 1..14`, `FX 1..4`,
`Predicador 1..4`, `Seguidores 1..4`).

Arreglado preguntándole al **disco** (`Directory.Exists`), que es la única fuente que no se
congela durante el batch. Las 55 borradas.

### 11.6 ✅ Verificado en Unity — Play Mode, escena real, casas reales

Con el Editor abierto en ESTE proyecto: **44 tests pasan, 0 fallan**; la escena se
reconstruye con cero errores; el aro se dibujó sobre casas generadas y el lazo cerró.

**El convenio de ángulo cierra de punta a punta.** Se abrió una puerta llamando a
`SkillcheckRunner.Begin`, o sea por el camino de eventos real
(`AttemptStarted` → `SkillcheckRing.Repaint` → `ArcPainter`), y la aguja quedó dentro de la
zona pintada. Sin esa prueba, un signo dado vuelta habría dado una zona correcta en los
números y en el lugar equivocado de la pantalla.

#### Los tiempos de una tirada, medidos (precisión 0.75, día 0, sin comitiva)

| medida | valor |
|---|---|
| ancho de zona | 0.653 rad = **37.4°** |
| velocidad de aguja | 1.95 rad/s → vuelta completa en **3.22 s** |
| espera hasta la zona | **1.09 s** |
| ventana buena | **0.335 s** |
| ventana perfecta | **0.120 s** |

⚠️ **120 ms de ventana perfecta.** Es apretado pero es lo que corresponde a un premio; el
respaldo es que la zona buena dura casi el triple. Número para playtest, no para tocar
ahora.

#### El salto de tramo se ve
Cinco puertas seguidas, todas perfectas:

| puerta | seguidores al abrir | eslabones | segundos ganados |
|---|---|---|---|
| 1–4 | 0 → 3 | 1 | 7.0 |
| **5** | **4** | **2** | **14.0** |

El vecino recién tira réplica en la puerta 5 (`"Estoy por salir."`): con un solo eslabón no
hay pausa, así que no hay nada que decir. Es correcto por diseño.

⚠️ **7 segundos por conversión perfecta contra un día de 72.** Es la primera vez que ese
número se puede medir. Con cadena de 2 son 14. El techo es `bonusCapSeconds = 16`. Si un
jugador bueno encadena, el día no termina más — pero decidirlo necesita el reloj del día,
que es fase 7.

#### Dos cosas que se vieron y no se tocaron

1. **El hueco de la aguja es real y grande.** Confirmado a ojo, no solo en la aritmética de
   §11.3: la punta frena bastante antes del arco. Se lee igual —la aguja *señala* el
   sector— pero no se tocan.
2. **El aro desaparece donde cruza el skyline.** El trazo es `#1D1638` y la silueta
   `#3A3050`: contraste **1.37:1**, cuando el mínimo utilizable para UI es 3:1. Contra el
   cielo claro y contra las fachadas se lee perfecto; contra la silueta, no.
   ⚠️ **Esto empeora con la fase 7**: el ciclo de día oscurece el cielo hacia la tarde y lo
   acerca al color de la silueta, así que la mitad de arriba del aro se va a ir apagando
   sola. La salida barata, si molesta, es un disco de fondo pintado por el mismo
   `ArcPainter` — ya tiene la textura y el pincel.

El aro tapa la fachada, la puerta y hasta el tendal mientras está en pantalla. **No es un
problema de juego**: para cuando aparece, las señales ya se leyeron y el timbre ya se tocó.
Queda anotado para la pasada de composición de §10.3.

### 11.7 El velo del skillcheck — y lo que el velo NO arregla

Se agregó un velo radial detrás del aro: `SkillcheckVeil` pinta con el mismo `ArcPainter`
una mancha oscura densa en el centro que se desvanece hacia afuera, en una textura de
192 px (más grande que el aro, porque el desvanecido necesita lugar por fuera para llegar
a cero). Opacidad, radios, color y escalones en el Inspector.

El desvanecido va **cuantizado en escalones** (8 por defecto) y no continuo. Una rampa de
alpha suave es correcta en 3D y ajena al pixel art; con pocos escalones el velo se lee como
parte del dibujo. Subiendo `steps` vuelve a ser continuo, así que la decisión está expuesta.

#### ⚠️ El velo NO arregla el contraste del aro. Lo empeora.

La instrucción era arreglar el 1.37:1 del trazo contra el skyline. **Un velo oscuro detrás
de un trazo oscuro no puede hacerlo**: el aro es `#1D1638`, casi negro, y oscurecer el
fondo lo acerca al aro en vez de alejarlo. Medido rindiendo la cámara a textura y barriendo
360°, comparando el trazo a r=56 px contra el fondo a r=62 px:

| opacidad | aguja `#F3ECE0` a r=20 (peor/prom) | zona `#6F8A5E` a r=50 (peor/prom) | **aro** (prom) |
|---|---|---|---|
| 0.00 (sin velo) | 1.00 / 6.12 | 1.01 / 2.78 | **3.73** |
| 0.35 | 1.48 / 7.11 | 1.04 / 2.60 | 3.19 |
| 0.55 | 2.05 / 7.97 | 1.05 / 2.54 | 2.86 |
| **0.75** | **3.31** / 9.29 | 1.08 / 2.69 | 2.57 |
| 0.90 | 6.11 / 11.25 | 1.14 / 3.02 | 2.35 |

**Lo que el velo sí arregla es lo que importa para jugar.** La aguja es lo que se sigue con
la vista, y sin velo tiene puntos donde queda en **1.00:1 — literalmente invisible** contra
el fondo. El velo la saca de ahí. El aro es un marco: que se apague contra el skyline no
impide apuntar.

**Por eso el default quedó en 0.75 y no en 0.55**: es donde la aguja cruza el 3:1 mínimo
contra el PEOR fondo posible. No es gusto, es la primera fila de la tabla que lo pasa.

Si además se quiere recuperar el trazo del aro, el velo no es la palanca. Las que sí:
- subirle el valor al `ui_skillcheck_aro` en el arte, o
- pintar una pista clara bajo el canal con el mismo `ArcPainter`, que ya tiene el pincel.

⚠️ **Un velo semitransparente produce colores fuera de la paleta de 29 por construcción**:
la mezcla entre mundo y velo cae donde caiga. Lo que se mantiene en paleta es el color del
velo (`#1D1638`), no el resultado. La alternativa estricta sería tramar con colores de la
paleta en vez de usar alpha.

---

## 12. Fase 7 — tiempo, luz y comitiva

### 12.1 `DayClock` (7a) ✅ 11 tests

El reloj es la economía entera del juego: cada puerta cuesta tiempo y cada conversión lo
devuelve. Por eso es clase plana y no un `MonoBehaviour`.

**Dos progresos, no uno.** `Progress` es el instantáneo y sube y baja con los bonos;
`SunsetProgress` **nunca baja**. Los gradientes de cielo y luz usan el segundo: un cielo que
se aclara al convertir se lee como un bug y no como un premio. El sol no vuelve para atrás.

**El bono topea en la duración inicial (72 s).** Sin techo, encadenar conversiones dejaría
un colchón imposible de gastar y la presión del reloj desaparecería justo para el jugador
bueno, que es a quien hay que apretar.

⚠️ **Esto resuelve solo lo que quedó abierto en §11.6**: ahí preocupaba que 7 s por
conversión perfecta contra un día de 72 estirara la partida sin límite. Con techo no puede:
el bono se cobra únicamente cuando hay lugar donde ponerlo, o sea cuando hace falta. Sigue
sin estar jugado, pero ya no es una fuga abierta.

`Advance` devuelve `true` en el **único** cuadro en que el día termina, para que quien
escuche dispare el final una sola vez sin llevar su propio flag.

### 12.2 Lo que falta de la fase 7
- **7b, luz**: escribir `DayCycleConfig.SkyColorAt` sobre `Camera.backgroundColor` —⚠️ no
  sobre la luz global, ver §3.8b—, el color y la intensidad sobre la `Light2D` global, y el
  encendido progresivo de ventanas y faroles pasado `lightsOnFraction`.
- **7c, comitiva**: la fila de seguidores detrás del predicador, suscrita a
  `SkillcheckRunner.FollowersChanged`, con `spacingPixels` y el retardo por posición que la
  hace moverse como víbora en vez de como bloque.

⚠️ **El cielo de noche es `#1D1638`, exactamente el color del aro del skillcheck.** Al final
del día el aro va a ser invisible contra el cielo mismo, no solo contra la silueta del
skyline. Es el mismo hallazgo de §11.7 llevado al peor caso, y el velo no lo tapa: hay que
tocar el arte o pintar una pista clara.

### 11.8 El contorno del aro — la garantía que no depende del fondo

`SkillcheckOutline` pinta con el mismo `ArcPainter` dos anillos claros fijos (`#F3ECE0`)
pegados por fuera de cada trazo oscuro de `ui_skillcheck_aro`: hacia adentro del círculo
interno y hacia afuera del externo, que es donde termina la figura.

**Un trazo bicolor se lee contra cualquier fondo.** Es lo mismo que hace el outline de los
sprites, y por eso los personajes no desaparecen contra una pared. La alternativa —teñir el
aro según la hora— obliga a mantener sincronizados dos gradientes que viven en assets
distintos, y el cielo de noche de `DayCycleConfig` es **exactamente** `#1D1638`, el color
del aro: cualquiera que toque uno sin acordarse del otro lo vuelve a hacer desaparecer.

⚠️ **No se pinta en la textura de la zona**: ese renderer se tiñe al acertar y al fallar
(`hitTint`/`missTint`), así que el contorno dejaría de ser fijo — se pondría rojo justo en
el cuadro en que hay que leer el resultado. Va en su propia capa, orden 99.

Reparto de responsabilidades, que ahora es limpio:
**el velo separa la UI del mundo; el contorno garantiza que se vea.**

#### Medición, con el peor fondo posible

Métrica: para cada uno de 360 ángulos, el salto de luminancia **más fuerte** del perfil
radial que cruza el borde del aro. Responde "¿se ve un borde acá?" sin depender de que el
sample caiga justo sobre un trazo de 1 px.

| | promedio | ángulos bajo 3:1 |
|---|---|---|
| día, sin contorno | 3.83:1 | 110 / 360 |
| **día, con contorno** | **11.02:1** | **35 / 360** |
| noche, sin contorno | 2.71:1 | 219 / 360 |
| **noche, con contorno** | **10.61:1** | **74 / 360** |

Con contorno, **la noche (10.61) se lee mejor que el día sin contorno (3.83)**: el aro dejó
de depender del fondo, que era el objetivo.

#### ⚠️ El grosor es un ancho de RADIO, no una cantidad de píxeles
Con `thickness = 1` el anillo sale **agujereado**: no en todos los ángulos cae un centro de
píxel dentro de la banda. Medido sobre el cielo de noche:

| grosor | promedio | ángulos bajo 3:1 |
|---|---|---|
| 1.0 | 10.61:1 | 74 / 360 |
| 1.5 | 14.27:1 | 7 / 360 |
| **2.0** | **14.61:1** | **0 / 360** |

**Por eso el default es 2 y no 1.** Un contorno con agujeros no es una garantía. Si 2 se ve
muy pesado, 1.5 deja 7 ángulos de 360 y está en el Inspector.

#### Dos errores de medición que costaron tres pasadas
1. **Sondear a radio fijo no sirve para un rasgo de 1–2 px**: el sample cae al lado del
   trazo en muchos ángulos y da 1.00:1 por comparar el fondo contra sí mismo. Hay que
   recorrer el perfil radial y quedarse con el salto más fuerte.
2. **Rendir la cámara a una `RenderTexture` de otro aspect deforma la medición.** La cámara
   corre a 2.170 y se estaba rindiendo a 16:9, así que el círculo de sondeo era una elipse
   contra el aro. Se corrigió muestreando por `WorldToViewportPoint` en vez de escalar
   `WorldToScreenPoint` a mano.

### 12.3 Luz y comitiva (7b y 7c) ✅ verificadas en Play Mode

**`DayDirector`** (Gameplay) es el dueño del reloj: lo corre, le pasa el `TimeDelta` de cada
puerta y avisa `DayEnded` una sola vez. No dibuja nada.

**`DayCycleView`** (Presentation) escribe el cielo sobre `Camera.backgroundColor` —⚠️ NO
sobre la luz global, §3.8— y el color y la intensidad sobre la `Light2D`. Lee
`SunsetProgress`, el que no baja.

**`FollowerParade`** (Presentation) dibuja la fila. Cada seguidor va a donde estaba el
predicador hace `delayPerPosition × su puesto`, más un offset de `spacing × puesto`. Por eso
la fila se estira al arrancar y se amontona al frenar en una puerta.

⚠️ **El hueco real entre seguidores es `spacing + velocidad × delay`, no `spacing`.** Con los
valores actuales: 22 px de separación + 152 px/s × 0.06 s = **31 px medidos**. Si se quiere
que el hueco a paso firme sea exactamente 22, hay que bajar `spacingPixels` a 13.

Solo dibuja: la CUENTA de seguidores la lleva `SkillcheckRunner`, porque sube al convertir y
baja al fallar, y eso es economía y no presentación (§11.4).

#### Corrida completa, medida
| | |
|---|---|
| cinco puertas perfectas | conversiones 1→5, seguidores 1→5, **5 dibujados** |
| tiempo tras las puertas | 57.9 → 64.9 → 71.9 → **72.0 → 72.0 → 72.0** |
| al caer la noche | atardecer 1.000, cielo `#1D1638`, luz `#3A3050` a 0.45 |

**El techo del bono se ve trabajando**: las puertas 3, 4 y 5 no sumaron nada porque el
colchón ya estaba lleno. Cero errores de consola en toda la corrida.

⚠️ `Unity_SceneView_Capture2DScene` **no rinde el `backgroundColor` de la cámara**, así que
en las capturas el cielo sale blanco aunque el color sea el correcto. El cielo se verificó
por número, no por imagen.

### 12.4 El encendido de ventanas — estuvo bloqueado, ya está cerrado

`DayCycleConfig.WindowLightAt` y `lightsOnFraction` existen y quedaron **sin consumir**.

El único arte aditivo del proyecto —`fx_farol_luz` y `fx_ventana_luz`— pertenece a
**señales** (`LuzEntrada` y `TvParpadeando`). Manejar su alpha con la hora del día haría que
una señal aparezca o desaparezca sola a media partida, y eso **rompe la regla central de
§3.3: las señales siempre dicen la verdad**. Un jugador que aprendió a leer una casa la
vería cambiar de estado sin que nadie tocara nada.

Hacía falta un sprite aditivo de ventana iluminada que NO fuera una señal. **Llegó como
`fx_ventana_encendida` (§12.5) y quedó enchufado (§12.6).**

### 12.5 `fx_ventana_encendida` — generado por código, no dibujado

El asset que faltaba para §12.4. Es un gradiente matemático puro, así que se generó
programáticamente en vez de dibujarse: el degradado sale exacto y sin banding por
construcción, y se puede volver a generar cambiando dos números.

| pedido | medido |
|---|---|
| 60×60 px | ✅ 60×60 |
| `#FFE6B0` | ✅ **un solo RGB** en todo el sprite |
| centro ~70% de opacidad | ✅ alpha 178 = **69.8%** |
| óvalo, más ancho que alto | ✅ halo 52×42, relación **1.24** |
| llega a cero antes del borde, 4 lados | ✅ alpha 0 en los cuatro; margen L4 R4 T9 B9 |
| sin bandas ni escalones | ✅ salto máximo **10** niveles, 27 escalones en el eje mayor |

La caída es `(1 − d²)²` sobre la distancia elíptica normalizada: vale cero **y tiene
pendiente cero** en el borde del halo, así que no queda un corte donde termina.

#### ⚠️ Dos trampas de verificación, de la misma familia que las de §11.8
1. **El encoder PNG de GDI+ no escribe RGB exacto**: la primera versión salió con 55
   colores distintos y el centro en `#FEE6B0`. Se regeneró con `Texture2D.EncodeToPNG`,
   que escribe RGBA32 tal cual.
2. **GDI+ tampoco LEE bien**: premultiplica al abrir y devuelve RGB destrozado donde el
   alpha es bajo (`#FFFFFF`, `#FFFF7F`). Seguía reportando 55 colores sobre el archivo ya
   corregido. La verificación buena es `Texture2D.LoadImage` + `GetPixels32`.

La lección repetida: **cuando una medición no se mueve, sospechar del instrumento antes que
del arte.** Pasó con el aspect de la RenderTexture y pasó de nuevo acá.

### 12.6 El encendido, ya enchufado

`HouseInstance.SetWindowLight(0..1)` escala el alpha del halo; el sprite ya trae horneada su
opacidad máxima. `WindowLightView` lo aplica a todas las casas activas cada `LateUpdate`.

**Va en la ventana del TELL y no en la de señales.** La de señales puede convertirse en
`env_ventana_tv`, que ES una señal con su propia capa aditiva: un halo que se enciende con
la hora encima de una señal la haría parecer que cambia sola, y eso es exactamente lo que
§3.3 prohíbe.

El halo va en la capa **Houses orden 12**, no en FX. FX se dibuja delante de todo, incluida
la reja, y el halo quedaría flotando por delante del portón en vez de saliendo de adentro.
Es el mismo motivo por el que el farol de las señales tampoco va en FX.

`WindowLightView` es una clase aparte de `DayCycleView` porque son dos cosas distintas: una
pinta cielo y luz, la otra le habla al pool de casas. Juntarlas obligaría a que quien pinta
el cielo conozca al spawner sin necesitarlo.

#### Cómo se verificó sin poder correr el reloj
⚠️ **El Editor sin foco no tickea de forma confiable**: con `timeScale` en 40 y sin pausa, el
reloj no avanzó ni un segundo entre dos llamadas del puente. Verificar el atardecer
esperando no es reproducible.

La cadena se probó en los dos sentidos, sin tocar el reloj:

| paso | resultado |
|---|---|
| arranque, de día | apagadas |
| prendidas **a mano** a 0.8, salteando al view | 0.80 |
| un cuadro del view | **apagadas** — el view las apagó porque es de día |
| con `lightsOnFraction = 0`, un cuadro del view | las prendió solo |

El paso 3 es el que importa: prueba que el view **maneja** el valor y no que simplemente no
estorba. Después se forzó el estado de noche a mano para mirarlo, y ahí el halo se lee
claramente contra la casa oscurecida.

⚠️ **Ojo con tocar assets durante Play Mode**: los `ScriptableObject` NO se revierten al
salir, a diferencia de los objetos de escena. El `lightsOnFraction = 0` del paso 4 se
restauró en la misma corrida y se confirmó en 0.6 después de salir.

---

## 13. Fase 8 — input y UI

### 13.1 `OneButtonInput` (8a) — sin bindings estáticos

⚠️ **No se ata a `<Gamepad>/buttonSouth` ni a ningún binding por dispositivo.** Escucha
`InputSystem.onAnyButtonPress`, que es agnóstico: le da igual si el botón viene de un
teclado, de un mouse, de un `Gamepad`, de un `Joystick` genérico o de HID crudo. Es la
única forma de que un encoder de arcade funcione sin saber de antemano cómo se presenta.

**Todo se calcula al consultarlo**, igual que el provisional: el apretón sella el número de
cuadro (`Time.frameCount`) desde el callback del Input System, que corre ANTES de cualquier
`Update`. Quien pregunte, cuando pregunte, ve lo mismo. Sin eso, el resultado dependería del
orden de ejecución entre componentes.

El gesto mantenido va en **tiempo no escalado**, para que una pausa o un `timeScale`
distinto no le cambien el largo.

#### El filtro `Accepts`, y por qué es `public static`
La ventana de diagnóstico usa **esta misma función** para decir si un apretón fue aceptado.
Con dos copias del filtro, el diagnóstico podría decir que todo está bien mientras el juego
descarta el botón — que es exactamente el escenario que la ventana viene a evitar.

Rechaza tres cosas: lo que no es `ButtonControl`, lo sintético y lo ruidoso.

⚠️ **El rechazo por ruidoso es el riesgo vivo del gabinete.** Se descarta porque un control
que cambia solo tocaría timbres fantasma, pero si algún día un encoder aparece con sus
botones marcados como ruidosos, **el gabinete queda mudo**. Por eso la ventana lo dice con
todas las letras en vez de mostrar un "descartado" pelado.

#### Verificado
| control | resultado |
|---|---|
| `/Keyboard/space`, `/Keyboard/enter` | ACEPTADO |
| `/Mouse/leftButton`, `/Mouse/rightButton` | ACEPTADO |
| `/Keyboard/anyKey` | descartado (sintético: dispara con cualquier tecla) |
| `/Mouse/position`, `/Mouse/scroll` | descartado (no son botones) |

El camino completo se probó con un evento sintético (`QueueStateEvent` + `InputSystem.Update`):
el apretón llega al observador con su path. ⚠️ **Falta probar con un encoder real**: en esta
máquina el Input System solo ve teclado y mouse.

⚠️ El primer filtro daba por bueno `/Mouse/position`, que es un `Vector2Control`. No rompía
nada —`onAnyButtonPress` solo entrega botones— pero el diagnóstico lo habría etiquetado como
aceptable, o sea habría mentido justo cuando hay que creerle.

### 13.2 Ventana de diagnóstico (8c) — `Tools/Buenos Días/Diagnóstico de input`

Contesta UNA pregunta: **¿detección o binding?** Son dos fallas que se sienten igual —el
botón no hace nada— y se arreglan en lugares opuestos.

| lo que se ve | qué es |
|---|---|
| el dispositivo NO figura en la lista | **detección**: driver, cable o puerto. El juego no participa |
| figura, pero apretar no registra nada | el encoder no manda ese botón, o lo manda como eje |
| registra y dice **DESCARTADO** | **binding**: llega y el juego lo filtra. Con el motivo escrito |

Tres secciones: dispositivos con layout e interfaz, log de apretones del más nuevo al más
viejo con aceptado/descartado y el motivo, y qué está apretado ahora mismo.

**Registra TODO, aceptado o no.** Un log que solo mostrara lo aceptado no podría distinguir
"no llega" de "llega y se descarta", que es la pregunta entera.

Anda en Edit Mode: se puede enchufar el gabinete y probarlo **sin darle Play**.

### 13.3 Fase 8 cerrada — qué quedó adentro y qué se movió
- **8a** `OneButtonInput` ✅ · **8c** ventana de diagnóstico ✅ · **8b** barra de tiempo ✅
- **Contador de conversiones: NO VA.** Decisión de diseño tomada, ver §13.6.
- **Selección de religión: se movió a la fase 9.** Necesita la capa de estado de partida, que
  es la misma que piden los finales. Ver §13.6 y §14.

### 13.4 Dato del gabinete: anduvo en un build HTML

⚠️ El botón de arcade es prestado y **no está disponible para probar**. Pero se sabe que
funcionó en un build HTML, o sea que lo vio la **Gamepad API del navegador**.

Eso acota mucho el riesgo de §13.1: la Gamepad API solo expone dispositivos que se presentan
como gamepad estándar, no HID crudo con usages raros. Un dispositivo así en Unity entra por
el layout `Gamepad`, donde los botones **no** están marcados como ruidosos. Lo más probable
es que ande directo.

El riesgo del control ruidoso queda anotado igual, y la ventana sigue siendo la respuesta:
el día que aparezca el botón, en vez de adivinar por qué no responde se enchufa y se mira.

✅ **Y desde §13.7 la ventana además lo RESUELVE**: el filtro de ruidosos se afloja con un
checkbox, sin recompilar. Es el riesgo cerrado sin haber tenido nunca el botón en la mano.

### 13.5 Barra de tiempo (8b, parte hecha)

Se dibuja con `SpriteRenderer.size` y **no con escala**: escalar en pixel art rompe la
grilla, y una barra que se vacía cambiaría de escala en cada cuadro. Con `size` cambia la
malla y el texel sigue midiendo un píxel. El ancho se redondea a píxeles enteros.

⚠️ **La altura se recalcula cada cuadro desde `orthographicSize`, no se deja fija.** La
Pixel Perfect Camera CAMBIA el tamaño ortográfico en runtime según la ventana: se midió
**3.688** donde el diseño dice 3.375. Una barra con offset fijo se despega del borde de
pantalla en cuanto la ventana no tiene la relación de referencia. Anclada, quedó a 11 px del
borde con la cámara corriendo a 3.688.

El relleno se vacía **desde la derecha**: su borde izquierdo queda clavado en −80 px y lo que
cambia es el ancho. Con el centro fijo se vaciaría por los dos lados y no se leería como un
reloj.

El parpadeo es una **onda cuadrada** y no un desvanecido: un fundido entre dos colores
inventa tonos fuera de la paleta y se lee peor de reojo, que es como se mira una barra.

#### Medido
| fracción de día | relleno | borde izquierdo | entero |
|---|---|---|---|
| 1.000 | 160 px | −80 px | sí |
| 0.750 | 120 px | −80 px | sí |
| 0.500 | 80 px | −80 px | sí |
| 0.250 | 40 px | −80 px | sí |
| 0.100 | 16 px | −80 px | sí |

Bajo el umbral de 12 s el relleno pasa a `#A8455A` y alterna con el hueso a 4 Hz.

### 13.6 Las dos decisiones de 8b — tomadas

#### ✅ Contador de conversiones: NO VA. La comitiva es el contador.

El proyecto no tiene fuente pixel-art, y `ui_folleto_00/22/45/67` **no sirven**: esos números
son rotaciones (0°, 22°, 45°, 67°) de un folleto volando, no dígitos.

Se decidió **no dibujar ningún número**. Cada conversión suma un seguidor visible caminando
atrás del predicador: el marcador ya existe y está en el mundo, no en una capa de UI encima.
Un número arriba diría lo mismo dos veces, y encima obligaría a mantener una fuente entera
para decirlo peor. **El score camina atrás tuyo.**

Consecuencia práctica: si algún día hace falta una cifra exacta —una pantalla de resultados,
por ejemplo— **el problema de la fuente vuelve**. `SkillcheckRunner.Converts` tiene el número;
lo que no hay es con qué escribirlo. Anotarlo antes de prometer texto en la fase 9.

#### ✅ Selección de religión: se hace en la fase 9, con los finales.

Hoy el juego arranca caminando: no hay menú, ni pantalla de resultados, ni transición. La
selección de religión necesita exactamente la misma capa de estado de partida que los tres
finales. Hacerla a medias en la fase 8 y rehacerla en la 9 sería trabajo tirado, así que la
fase 9 **arranca por esa capa** y la selección cuelga de ahí.

El gesto de 0.6 s ya está listo y probado en `OneButtonInput.HoldProgress`, esperando quién lo
consuma.

### 13.7 La ventana de diagnóstico ya RESUELVE, no solo informa

Hasta acá la ventana contestaba "¿detección o binding?" pero, si la respuesta era
"el encoder marca sus botones como ruidosos", la única salida era abrir un `.cs` con el
gabinete enchufado. Ahora trae las dos herramientas para resolver en el momento.

#### Aceptar controles ruidosos

Un checkbox en la barra de la ventana afloja el filtro **del juego** en caliente:
`OneButtonInput.Accepts` consulta `OneButtonInput.AcceptNoisyControls`.

- **Los sintéticos NO se aflojan nunca.** Ahí el problema no es el hardware sino que el
  control no es un botón: `Keyboard.anyKey` dispara con cualquier tecla y las direcciones de
  un stick son el stick. Aflojarlos haría que mover la palanca tocara timbres.
- **Vive en `EditorPrefs`, no en un asset.** Es una decisión de la máquina que está probando
  el gabinete, no del proyecto: en un asset se colaría en un commit y viajaría a todas las
  máquinas.
- **Se restaura después de CADA recarga de dominio** con `[InitializeOnLoadMethod]`. Sin eso,
  darle Play apagaría la palanca sola y el gabinete volvería a quedar mudo justo al probarlo.
  Corre aunque la ventana nunca se haya abierto.
- El motivo "RUIDOSO" del registro ahora **dice qué hacer**: *"→ tildá 'Aceptar controles
  ruidosos' arriba y volvé a apretar"*.

⚠️ **Para una BUILD el checkbox no sirve**: `EditorPrefs` no existe fuera del Editor. Para eso
está el campo `acceptNoisyControls` del componente `OneButtonInput`, que **solo prende la
palanca y nunca la apaga** — si la apagara, borraría el rescate hecho desde la ventana justo
al darle Play.

#### Copiar informe

Deja en el portapapeles, en texto plano: versión de Unity y sistema operativo, si la palanca
está prendida, los dispositivos con **layout, interfaz, fabricante, producto y si están
activos**, el registro de apretones con veredicto y motivo, y qué hay apretado en ese momento.
Un layout mal transcripto a mano manda a buscar el problema al lugar equivocado.

El veredicto de cada línea del registro es **el que valía al momento del apretón**, y el
informe lo aclara: si se afloja el filtro después, las líneas viejas siguen diciendo lo que
dijeron, que es lo que hace falta para entender qué pasó.

#### ✅ Verificado simulando el gabinete que falla

Se registró un layout `EncoderRuidoso` que extiende `Gamepad` y **marca su `buttonSouth` como
ruidoso**: exactamente el modo de falla que estaba anotado como riesgo.

| | `Accepts` |
|---|---|
| botón ruidoso, palanca apagada | **False** ← el gabinete mudo |
| botón ruidoso, palanca prendida | **True** ← rescatado |
| control sintético, palanca prendida | **False** ← no se afloja nunca |

Y la persistencia, que es lo que más importa porque darle Play recarga el dominio: se prendió
la palanca, se forzó una recarga con `EditorUtility.RequestScriptReload()` y **quedó prendida
del otro lado**. El informe se generó completo y se comprobó que llega al portapapeles.

Estado dejado en la máquina: palanca apagada y la clave de `EditorPrefs` borrada.

---

### 13.8 🔴 PENDIENTE DE REVERTIR — `acceptNoisyControls` tildado para la prueba del gabinete

**Fecha de la prueba: miércoles 12 de agosto de 2026. Después hay que apagarlo.**

`Assets/Scenes/Game.unity` → objeto **`Predicador`** → componente `OneButtonInput` →
`acceptNoisyControls: 1`.

Esto va contra la regla de polaridad de §14.6 —una bandera que hay que acordarse de apagar
termina en la build— y se hace **a sabiendas**, porque acá el cálculo de riesgo se da vuelta:

- Filtro flojo de más, en el peor caso: **un timbrazo fantasma**.
- Filtro apretado sin poder aflojarlo, en una build sin ventana de diagnóstico: **no hay demo**.

La prueba se va a hacer con el Editor abierto, que es donde está la ventana; la build es solo
respaldo por si Unity no arranca. El campo existe para ese respaldo.

#### Cómo revertirlo

Destildar el campo en el Inspector y guardar la escena. El diff tiene que volver a quedar en
cero: la única línea que agrega este cambio es `acceptNoisyControls: 1`.

⚠️ **Al guardar la escena, revisar el diff antes de commitear.** La Pixel Perfect Camera
reescribe `orthographic size` y `m_NormalizedViewPortRect` de la cámara (§3.8), y cualquier
save los arrastra: en este commit se colaron `3.375 → 3.6875` y un viewport de `0/1` a
`0.00097/0.997`. Se revirtieron a mano y la escena se recargó desde disco.

#### El componente NO está en un prefab

El único prefab del proyecto es `Assets/Prefabs/World/House.prefab`. `OneButtonInput` vive
suelto en la escena, sobre `Predicador`. Hay exactamente **una** instancia.

#### Build Settings: una sola escena

`Assets/Scenes/SampleScene.unity` —la de ejemplo que crea Unity al abrir el proyecto— estaba
habilitada en Build Settings y se sacó. No rompía nada porque `Game.unity` es el índice 0 y la
build arrancaba bien, pero era peso muerto adentro del `.exe`. **La build tiene que tener una
sola escena.** `RunDirector.Restart()` recarga por `gameObject.scene.buildIndex`, que sigue
siendo 0.

#### ⚠️ El proyecto tiene `DisableDomainReload` prendido

`EditorSettings.enterPlayModeOptions = DisableDomainReload`. Dos consecuencias que importan:

- **Los `static` NO se resetean al entrar ni al salir de Play.** `AcceptNoisyControls` es el
  único static del proyecto, así que el daño está acotado, pero si se prende en una sesión de
  Play queda prendido para la siguiente.
- **`[InitializeOnLoadMethod]` no corre al darle Play**, solo al recompilar o al abrir el
  Editor. Sigue haciendo falta —es lo que restaura la palanca tras un recompilado— pero no es
  lo que la salva de un Play.

#### ✅ Medido: el campo prende la palanca de verdad

Con `EditorPrefs` borrado y el static forzado a `false`, se entró a Play y se midió el EFECTO
sobre el filtro, no la bandera, registrando el layout `EncoderRuidoso` de §13.7:

| | `Accepts(buttonSouth)` |
|---|---|
| palanca prendida por el campo de la escena | **True** |
| misma sesión, palanca forzada a `false` | **False** |
| sintético (`Keyboard.anyKey`), palanca prendida | **False** |

`runInBackground` se restauró a `False` después de salir de Play.

---

## 13bis. El juego DEJÓ de ser de un botón

El control pasa a ser un objeto físico con **tres entradas**. Esto no es una capa
encima de la FSM de un botón: es un cambio de arquitectura, decidido con el diseñador.

### Las tres acciones

| | qué es | entrada física |
|---|---|---|
| **TIMBRE** | pulso | 🔒 **click izquierdo del mouse** |
| **LIBRO** | pulso | `Enter` |
| **FELPUDO** | **ESTADO sostenido**, no pulso | `Space` |

🔒 El timbre está **soldado**: es la placa de un mouse con el pulsador del timbre de
pared en el switch del botón izquierdo. Cambiarlo en el Inspector no cambia el fierro.
✅ Medido: `Accepts(/Mouse/leftButton)` = **True**.

⚠️ **Para el felpudo, nunca `Shift`, `Ctrl`, `Alt`, `Caps Lock`, `Tab`, `Esc` ni la
tecla de Windows.** `Shift` sostenida dispara el diálogo de *Sticky Keys* de Windows
ENCIMA del juego, `Alt` se roba el menú de la ventana, `Caps Lock` es un toggle y la de
Windows abre el menú Inicio. Con el pie apoyado en la plancha, cualquiera arruina la demo.

### La forma

La FSM dejó de preguntar "¿apretaron?" —que con tres entradas ya no significa nada— y
pregunta por ACCIONES. `IInputSource` tiene dos implementaciones que son **dos esquemas
de control distintos, no uno envolviendo al otro**:

- **`DeviceInputSource`** (objeto): un binding por acción. No necesita contexto: el
  jugador ya dice con el cuerpo qué está haciendo.
- **`SingleButtonInputSource`** (teclado): un botón que significa cosas distintas según
  `InputContext`. Con un botón el significado no puede venir del hardware —siempre es el
  mismo botón— así que viene del estado. **Todo el esquema vive en un solo `switch`**.

**El default es Teclado**, por la regla de polaridad: anda en cualquier máquina, y
`Objeto` es la bandera que hay que acordarse de prender. Olvidarse solo significa jugar
con el teclado. **No se autodetecta**: las dos modalidades usan los mismos dispositivos
—el control físico ES un teclado y un mouse para Windows— así que adivinar saldría mal
justo el día que importa.

`GameInput` va con `[DefaultExecutionOrder(-100)]`: todos preguntan desde su propio
`Update`, así que el estado del cuadro tiene que estar armado antes.

### La única asimetría real entre las dos modalidades

El felpudo es un **estado sostenido** en objeto y un **toggle** en teclado. No se puede
evitar: con un botón no se puede pisar la plancha y pegarle al skillcheck a la vez.

Y de ahí sale la decisión del diseñador: **no se puede abandonar a mitad del
skillcheck.** No es de tono, es de balance — si bajarse fuera gratis y fallar costara
tiempo y seguidores, el jugador se bajaría siempre que viera que iba a errar, y eso mata
el sistema de fallo entero junto con el castigo de tramo. Una vez que la puerta se abre,
está comprometido.

⚠️ **Pero al RESOLVER el skillcheck hay que leer el estado real del felpudo.** Si el
jugador se bajó por reflejo durante el check, tiene que empezar a caminar en ese mismo
instante y no quedar trabado esperando volver a subirse.

### 🔴 Lo que falta cablear

La capa está escrita y compila, pero **todavía no la consume nadie**:
`PreacherController`, `ReligionSelector`, `RunDirector` y `HoldGaugeView` siguen leyendo
`OneButtonInput` directo. `OneButtonInput` no se borra: pasa a ser el lector CRUDO del
botón, que es lo que usa la modalidad de teclado por dentro.

---

## 14. Fase 9 — estado de partida, selección de religión y finales

### 14.1 La capa de estado (9a) ✅ 16 tests

Tres etapas que nunca se solapan: **Selección → Jugando → Final → Selección**.

`RunStateMachine` es plano y testeable. Cada transición declara **desde dónde sale**, y una
que salga de otro lado devuelve `false` y no toca nada. Eso hace que los avisos repetidos
—el reloj avisando el final dos veces, el botón apretado de más— sean inofensivos **sin que
cada quien tenga que llevar su propio flag de "ya lo hice"**.

`RunDirector` es solo el enchufe: prende y apaga por Inspector los tres sistemas que solo
pueden correr jugando (`ReligionSelector`, `PreacherController`, `DayDirector`) y avisa la
etapa por evento. No prende ni apaga vistas: si lo hiciera, el juego pasaría a conocer a la
UI. Las pantallas se suscriben a `PhaseChanged` y se esconden solas.

El gateo se aplica en `Awake` y no en `Start`: Unity garantiza que todos los `Awake` corren
antes que cualquier `Update`, así que el reloj no llega a descontar un cuadro de día mientras
el jugador todavía está eligiendo.

#### El toque va AL SOLTAR, no al apretar

Un mantenido **empieza con un apretón**. Si la religión siguiente saliera al apretar, cada
confirmación cambiaría de religión antes de confirmar y el jugador terminaría siempre con la
de después de la que quería. Por eso `ReligionSelector` cicla al soltar, con una traba que
evita que el mantenido ya confirmado cuente además como toque.

#### Reiniciar es recargar la escena, no un `Reset()` por sistema

Con un reset a mano, el día que alguien agregue un sistema con memoria y se olvide de
vaciarlo, la segunda partida sale distinta de la primera por un motivo que nadie va a buscar
ahí. La escena es chica y vuelve a cargar al instante.

⚠️ El cache de `PaletteSwapper` es **estático y sobrevive a la carga de escena**: hay que
llamar a `ClearCache()` antes de recargar o cada partida deja colgadas las texturas generadas
de la religión anterior.

### 14.2 🚨 Cada partida traía la MISMA cuadra — arreglado con `GameConfig.SeedFor`

Las tres semillas (`HouseSpawner` 1234, `PreacherController` 5150, `SkillcheckRunner` 907)
eran enteros fijos del Inspector. Mientras no se podía reiniciar no se notaba; **en cuanto
existió el reinicio, el segundo jugador veía exactamente el mismo barrio que el primero.**

Ahora las tres pasan por `GameConfig.SeedFor(semilla)`, que mezcla con el reloj mediante XOR.
Se mezcla y no se reemplaza: si a todos les tocara el mismo número, los tres sistemas
quedarían sincronizados entre sí, que es justamente lo que las semillas separadas evitan.

⚠️ **El default es MEZCLAR.** `fixedSeedDebug` (apagado) devuelve las semillas del Inspector
tal cual y sirve para medir dos veces lo mismo. La bandera está al revés que
`infiniteTimeDebug` a propósito: **una bandera que hay que acordarse de apagar antes de la
build termina en la build; una que hay que acordarse de prender para medir, en el peor caso
arruina una medición.** Para reproducir cualquier medición vieja de este documento, prenderla.

| | casas | predicador | skillcheck |
|---|---|---|---|
| normal (medido) | 136753488 | 136749468 | 136754697 |
| `fixedSeedDebug` | 1234 | 5150 | 907 |

Las tres distintas entre sí, y las cuadras que generan también: anchos `221.9 / 223.4 / 252.3
/ 260.7 / 232.3 / 201.8` con 1234 contra `242.9 / 246.3 / 219.7 / 204.5 / 218.1 / 254.4` con
la semilla mezclada.

### 14.3 🚨 El predicador se deslizaba sin animación de caminata

Encontrado al armar la pantalla de selección. El controller `Predicador.controller` tiene sus
cinco estados y su parámetro entero `Estado`, pero **nadie lo escribía nunca**, así que el
Animator se quedaba en su estado por defecto (`Idle`) toda la partida: el predicador avanzaba
por la vereda en pose quieta.

Lo arregla `PreacherView`, que traduce estado de FSM a número de estado. Los números son
campos del Inspector, no constantes: son el contrato con un asset que se edita en la ventana
de Animator.

| `Estado` | clip | cuándo |
|---|---|---|
| 0 | Idle | eligiendo religión, en el final, y casa vacía |
| 1 | Walk | caminando |
| 2 | Espera | esperando en la puerta y atendiendo |
| 3 | Timbre | pose corta al tocar el timbre |
| 4 | Exito | pose corta al convertir |

El timbre y el éxito son **poses** que se pisan encima del estado de fondo y vuelven solas.
Hace falta: la FSM pasa a `Esperando` en el MISMO cuadro en que suena el timbre, así que sin
la pose el gesto no llegaría nunca a dibujarse.

### 14.4 `ReligionPalettes.cs` borrado — era una segunda fuente de verdad

Tenía las cinco religiones **hardcodeadas en código** —nombre, colores y los siete
multiplicadores— duplicando exactamente los cinco assets de
`Assets/ScriptableObjects/Religions/`. Iba en contra del criterio 10 (ninguna clase enumera
religiones) y era una trampa esperando: tocar el asset y no el código habría dado una partida
con la corbata de una religión y la velocidad de otra.

Se verificó que los valores coincidían antes de borrarlo, y que nada lo referenciaba. Se fue
también `PlayerReligion`, que era el ejemplo de uso. `PaletteSwapper` **se queda**: lo usa
`ReligionPreviewView` y sigue siendo la deuda anotada en §8.

Los colores de origen del sprite (`#F3ECE0` camisa, `#7A2F3D` corbata) ahora son campos del
Inspector de `ReligionPreviewView`, no constantes: son los del PNG, y si se redibuja al
predicador esto tiene que poder seguirlo sin abrir un `.cs`.

### 14.5 ✅ Verificado en Play Mode, partida completa

| momento | medido |
|---|---|
| arranque | etapa `Seleccion`, predicador `enabled false` en X=0, reloj `enabled false` con 72 s, barra escondida |
| selección | TESTIGOS → MORMONES → BUDISTAS con tres toques; sprite `chr_predicador_idle_swap` (paleta aplicada) y `Estado` 0 |
| medidor | riel 28×3 px a 46 px sobre los pies; relleno 13 px con progreso 0.464 (28 × 0.464 = 13.0 exacto) |
| confirmación | etapa `Jugando`, religión de la partida BUDISTAS, predicador y reloj encendidos, selector apagado |
| jugando | X=94.86, FSM `Caminando`, `Estado` 1, sprite `chr_predicador_walk_02_swap`, barra 112 px con 50.3 s de 72 (0.699 × 160 = 111.8) |
| noche | etapa `Final`, final `Crucifixion` con 0 conversiones, cielo `#1D1638`, atardecer 1.000, predicador y reloj apagados, `Estado` de vuelta en 0, barra escondida |
| reinicio | etapa `Seleccion`, predicador en X=0, reloj en 72 s, selector de vuelta en TESTIGOS, cuadra nueva |

Consola limpia. **71 tests, 0 fallan** (eran 55).

### 14.6 ⚠️ Tres trampas del instrumento al sintetizar input por el puente

Las tres dieron lecturas que parecían bugs del juego y no lo eran. Van anotadas porque
cualquier verificación futura de input se las va a comer de nuevo.

1. **El teclado real se resetea sin foco.** El Input System sincroniza los dispositivos
   nativos cuando el Editor pierde el foco, así que un apretón encolado a `Keyboard.current`
   se borra antes del cuadro siguiente. **Salida:** `InputSystem.AddDevice<Gamepad>()` crea
   un dispositivo sin backend nativo detrás, que conserva el estado que se le pone. De paso
   ejercita el camino del gamepad, que es el que más probablemente use el gabinete: el filtro
   aceptó `/PadDePrueba/buttonSouth` sin tocar nada.

2. **`PressedThisFrame` no sobrevive a un `InputSystem.Update()` a mano.** El sello es
   `Time.frameCount`, y un comando del puente corre FUERA del bucle de juego: el número de
   cuadro ya cambió cuando el juego lo consulta. **Salida:** encolar el evento y NO
   procesarlo; que lo levante el propio bucle. Con eso el reinicio disparó al primer intento.
   Lo que se apoya en estado (`IsHeld`) no tiene el problema.

3. 🚨 **`IsPressed()` lee un buffer distinto según quién pregunte.** El Input System tiene
   buffers de estado **por tipo de update**. Un comando del puente lee el buffer del Editor;
   el juego lee el Dynamic. Se llegó a leer `IsHeld false` y `HoldProgress 0.000` **mientras
   el medidor en pantalla se seguía llenando**. La medición no se movía y el arte estaba
   bien: el que mentía era el instrumento. **Salida:** medir el EFECTO en la escena (el ancho
   del relleno), no el input. Fue así como salió el 13.0 px exacto de §14.5.

### 14.7 Lo que falta de la fase 9

- ~~Los nombres de las religiones~~ — RESUELTO con la fuente de bitmap, ver §14.9 y §14.10.
- ~~Las cinemáticas de los tres finales~~ — RESUELTO, ver §14.12.
### 14.8 El intento de puerta salió a `DoorAttempt` — y lo que eso NO arregló

Se sacó el intento de puerta a su propia clase: quién vive ahí, con cuánta puntería se tocó
el timbre y el reloj de la espera. Es otra responsabilidad y otra vida —la FSM dura toda la
partida, un intento nace al tocar un timbre y muere al abrirse la puerta— y así se van cuatro
campos que solo tenían sentido en uno de los cuatro estados, sin nada que lo dijera.

De paso, **el asomo de cortina lo dispara el intento y no la FSM**: la cortina es de la casa,
y quien maneja los estados del predicador no tiene por qué saber que una casa tiene Animator.
Y el orden de las dos tiradas —ocupación primero, duración después, del mismo `System.Random`—
quedó escrito donde pasa, porque invertirlo cambia todas las partidas de una semilla dada.

Los tres finales de puerta (abortar, casa vacía, puerta atendida) hacían lo mismo copiado en
tres lugares; ahora pasan por `Abandon()`.

**71 tests verdes antes y 71 verdes después.**

#### ✅ Cerrado: la clase cumple. La regla cuenta líneas de CÓDIGO.

Medido: el cuerpo de la clase pasó de 210 a **209 líneas físicas**, de las cuales **126 son
código**. La regla de las 200 (§9) cuenta código, no líneas físicas: existe para prevenir
clases-dios, y 126 líneas de lógica no lo son. Desglose del cuerpo:

| | líneas |
|---|---|
| código | **126** |
| doc XML (`///`) | 25 |
| `[Header]` y `[Tooltip]` | 14 |
| comentarios `//` | 5 |
| en blanco | 39 |
| **total** | **209** |

O sea: **no es una clase de 209 líneas de lógica, es una de 126 con 83 de documentación y
respiración.** Partir la FSM en dos para ganar nueve líneas sí sería spaghetti —los estados
dejarían de estar en un solo lugar— y bajar sacando tooltips sería peor todavía.

El corte quedó igual porque vale por sí solo: la FSM dejó de tocar el Animator de la casa y
los cuatro campos que solo servían en un estado se fueron con él.

### 14.9 La fuente de bitmap 6×8 — generada, no dibujada

Hacía falta igual para cerrar la fase 9: los tres finales tienen texto (`NI UN ALMA`,
`EL BARRIO SE ORGANIZÓ`, `TE LLEVÓ CON ÉL`, `SE HIZO DE NOCHE`) y las réplicas de las
objeciones también. Decidirla en la pantalla de religiones la hace pagar dos veces.

**Una fuente de bitmap no es arte, es datos**: una máscara de bits por glifo. Por eso se
genera y no se dibuja, y por eso los glifos viven en
`Assets/Editor/Fonts/fuente_6x8.glifos.txt` y no dentro de un `.cs`: se editan con cualquier
editor, sin recompilar, y un glifo torcido se arregla mirándolo. **El orden del archivo ES el
orden del atlas**, una sola fuente de verdad: si el orden viviera aparte, agregar un glifo en
el medio correría todos los demás y nadie se enteraría hasta ver texto en jeroglíficos.

| | |
|---|---|
| celda | 6 × 8 px, monoespaciada |
| glifos | **58** — A-Z, 0-9, ÁÉÍÓÚÑÜ, ¿?¡!, `. , : - + × % ( ) /` y espacio |
| atlas | 16 × 4 celdas = **96 × 32 px**, `Assets/Sprites/UI/ui_fuente_6x8.png` |
| escalas | ×1 para la UI, ×2 para titulares |

#### 🚨 Reservar las filas del acento no alcanzaba: hay que separarlas

El aviso de no dejar las tildes para después era correcto, y aun así faltaba una fila. Con la
caja original —acento en 0-1, mayúscula en 2-6— el acento quedaba **pegado** a la letra:
la `Á` se leía como una A con un pincho encima y la `Ñ` como una N con una raya. El problema
no es el dibujo del acento sino que no había nada entre los dos.

La caja quedó así:

| filas | qué |
|---|---|
| 0-1 | acento |
| **2** | **separación**. Sin ella el acento toca la mayúscula. |
| 3-7 | mayúscula, cinco de alto. La línea base es la fila 7, la última. |
| col 0-4 | tinta |
| col 5 | separación entre letras: es monoespaciada, el hueco es parte del glifo |

**Lo que se pagó:** no queda fila bajo la línea base, así que la coma apoya en ella en vez de
colgar. En español la eñe vale más que la cola de una coma.

#### 🚨 La lección: además del instrumento, sospechar de la REPRESENTACIÓN

Las trampas de §14.6 son de instrumento: el aparato con el que se mide miente. Esta es otra
familia. Acá el instrumento estaba bien —la tabla dice exactamente qué píxeles hay— y lo que
engañaba era **la forma de mirarla**:

> En la tabla, el acento y la mayúscula parecen separados **porque están en filas distintas
> del archivo**. Eso es un artefacto de mirar una tabla. En pantalla no hay filas: hay
> píxeles, y los píxeles de la fila 1 y los de la fila 2 se tocan.

Una tabla separa lo que una pantalla junta. Vale para cualquier dato que se edite en un
formato distinto de aquel en el que se va a ver: **la representación con la que trabajás
introduce separaciones que el resultado no tiene**. La salida es la misma de siempre —mirar el
resultado, no la fuente— y por eso el bug apareció recién al renderizar `ÁÉÍÓÚÑÜ` en pantalla.

#### El atlas es una máscara BLANCA, y el color lo pone quien dibuja

Con el atlas ya pintado en hueso, cualquier otro color saldría de multiplicar dos colores y
no caería exacto en la paleta. Pintado en blanco, el tinte **es** el color.

#### ⚠️ El texto se dibuja en dos pasadas, y no es decoración

Medido, contra los fondos donde el texto realmente cae:

| fondo | hueso `#F3ECE0` | sombra `#1D1638` |
|---|---|---|
| cielo de mediodía `#A8B4C6` | **1.79:1** ❌ | 8.18:1 |
| pared beige `#D9C9A8` | **1.39:1** ❌ | 10.51:1 |
| pared verde `#8FA88C` | **2.20:1** ❌ | 6.66:1 |
| silueta del skyline `#3A3050` | 10.41:1 | **1.40:1** ❌ |
| cielo de noche `#1D1638` | 14.61:1 | **1.00:1** ❌ |

El hueso solo falla en tres de cinco. **La sombra gana exactamente donde el hueso pierde y
pierde donde el hueso gana**: son complementarios, y juntos el cartel se lee contra cualquier
fondo sin que nadie tenga que elegir el color del texto según dónde va a caer. Es el mismo
argumento del trazo bicolor del aro (§11.8), ahora medido de antemano en vez de después.

La sombra va corrida `(+1, −1)`: cae en el hueco de separación de la celda y no pisa la letra
siguiente.

#### El ×2 sale de los `pixelsPerUnit`, no de `localScale`

A PPU 16 cada texel mide dos píxeles de mundo, que es exactamente un ×2, y el transform se
queda en 1. Escalando el transform, cualquier redondeo de la cámara pixel-perfect corre medio
píxel y las letras quedan con los bordes rotos. El centrado se redondea a píxel entero por lo
mismo: a medio píxel el cartel tiembla al cambiar de largo.

#### ✅ Verificado

- PNG en disco, leído con `LoadImage` (no por la textura importada): **96×32, 979 bytes**.
- **Exactamente 2 valores RGBA**: blanco opaco (610 px) y blanco transparente (2462). Alpha
  duro, sin premultiplicar — la trampa de §12.5 no apareció.
- **0 píxeles mal** comparando los 58 glifos contra la tabla, celda por celda.
- 0 tinta en la columna de separación y 0 en la fila de separación.
- En pantalla: `TESTIGOS` a ×2 mide 96 px (8 × 6 × 2) y arranca en −48; el trueque a ×1 mide
  138 px y arranca en −69. Todos píxeles enteros.
- `¿QUÉ AÑOS? ¡25%! (A-B) 3×4 /+:.,` dibuja 32 letras para 32 caracteres: **ningún glifo
  faltante en el juego de signos**.

### 14.10 La pantalla de religiones ya dice qué elegís

El nombre a ×2 y una línea de trueque a ×1, los dos centrados y colgados de la cámara a altura
fija **desde el centro** —no desde un borde—, así que no les aplica el pozo del
`orthographicSize` de §3.8.

El trueque es un campo del asset (`ReligionDefinition.tagline`), no una tabla en código:

| religión | línea | ancho |
|---|---|---|
| TESTIGOS | TODO NORMAL. EL PATRÓN. | 138 px |
| MORMONES | CAMINÁS RÁPIDO, ZONA CHICA | 156 px |
| BUDISTAS | AGUJA LENTA, UNA OBJECIÓN MÁS | 174 px |
| EVANGELISTAS | ZONA ANCHA, ESPERÁS MÁS | 138 px |
| ASPIRADORAS | MÁS TIEMPO POR VENTA. SIN CIELO. | 192 px |

Todas entran en los 384 px de la pantalla. Los carteles se esconden solos fuera de la
selección: escuchan la etapa, nadie los prende.

### 14.11 ⚠️ Dos trampas más del instrumento

Van con las tres de §14.6. Las dos parecían bugs del juego.

4. **No se puede sintetizar un toque corto por el puente.** Cada llamada tarda segundos, así
   que un apretón que empieza en un comando y termina en el siguiente dura mucho más que los
   0.6 s del mantenido: **todos mis toques eran confirmaciones**. Se vio como "el carrusel no
   avanza" y en realidad el carrusel nunca llegó a avanzar porque la partida ya había
   arrancado. Para probar el toque hay que subir `holdSeconds` a un número grande primero.
5. **`FindObjectsByType` no ve los objetos inactivos.** Los carteles "desaparecieron" y en
   realidad estaban apagados, que es justo lo que tienen que hacer fuera de la selección. Hay
   que pasar `FindObjectsInactive.Include` para distinguir "no existe" de "está apagado".

Y un aviso que no es un bug: **editar scripts sin salir de Play Mode** llena la consola de
*"The referenced script (Unknown) on this Behaviour is missing!"*. Es la escena de Play
apuntando al assembly viejo durante el domain reload; en la escena guardada se midieron **0
componentes rotos** y con un ciclo limpio no vuelve a aparecer.

### 14.12 Las cinemáticas de los tres finales (9b)

Cada final es un **asset**: titular, segunda línea, color de telón y una lista de piezas con su
sprite, su posición en píxeles desde el centro, su orden de dibujo, cuándo entra, cuánto se
corre por segundo, cuánto gira y a cuántos cuadros por segundo se anima.

⚠️ **Pero el asset lo ESCRIBE `EndingCatalogSeeder`, y lo reescribe entero cada vez que se
construye la escena.** Acá decía "agregar o retocar un final es tocar datos" y era falso: tocar
el `.asset` a mano sirve hasta que alguien corre *Fase 1*, y ahí `Reset(so)` le vacía la lista
de piezas y la vuelve a llenar desde el código. Se descubrió componiendo la ascensión: se
agregaron cuatro folletos al asset, se reconstruyó la escena para probar otra cosa y a la vuelta
había cinco capas en vez de nueve, sin un solo error en ninguna parte.

→ Es de la familia *generador contra artefacto* (§14).

**Lo que vale: el seeder es la fuente de verdad y el asset es su salida.** Retocar un final en el
Inspector sirve para MIRAR —correr una pieza y ver dónde queda, que es la única forma honesta de
componer— y el número que se encontró así se copia al seeder. Es lo mismo que ya pasa con las
banderas de la escena (§15.6): reconstruir borra lo hecho a mano, y lo único que salva es saberlo.

`EndingView` no decide cuál: escucha `RunDirector.EndingReached`, que ya trae el final resuelto
y las conversiones. Los titulares van en **×2** y entran 1.2 s después de la imagen: si entran
juntas se lee el cartel y no se mira el dibujo.

> **Sobre Timeline.** La fase 9 estaba anotada como "finales con Timeline". Se hizo con datos
> en un ScriptableObject: un Timeline por final serían tres assets binarios que solo se editan
> con la ventana abierta y que no se pueden medir desde un comando. Con la lista de piezas,
> cada número está en el Inspector y la verificación es leer el asset. No cambia nada de cómo
> se juega.

#### Los tres

| | titular | telón | piezas |
|---|---|---|---|
| **Crucifixión** (0 conversiones) | NI UN ALMA | `#EF7250`, naranja de atardecer | cruz, silueta, maletín, 3 cuervos, loma |
| **Se hizo de noche** (1–9) | SE HIZO DE NOCHE + `{n} ALMAS` | **ninguno** | ninguna |
| **Ascensión** (10+) | TE LLEVÓ CON ÉL + `{n} ALMAS` | `#3A3050` | nubes, rayos, haz, silueta, destello |

**El final del medio no tapa nada, y es la mejor decisión de las tres.** La calle ya quedó de
noche y con las ventanas prendidas: es exactamente la imagen que corresponde. Taparla con un
color plano sería borrar lo que construyó el ciclo de día de la fase 7. Sale gratis y es la
que mejor se ve.

Los tres cuervos son **la misma animación de tres cuadros desfasada**, no tres dibujos: los
PNG `cine_cuervo_00/01/02` son un aleteo, y "3 cuervos" del brief son tres instancias.

#### ✅ Verificado con los tres corriendo

Se forzó cada uno bajando los umbrales de `GameConfig` y devolviéndolos después; verificados
en disco: `crucifixionBelow 1`, `ascensionAtLeast 10`, y `ResolveEnding` da
0→Crucifixión, 1→Noche, 9→Noche, 10→Ascensión.

Dos correcciones que salieron de mirar, no de calcular:

- **La costura de las nubes.** El sprite de nubes trae su PROPIO relleno de cielo, medido en su
  fila inferior: `#3A3050`. Con el telón en violeta de noche, el borde de abajo del sprite
  dibujaba una raya horizontal que se leía como un horizonte inexistente. El telón de la
  ascensión pasa a `#3A3050` y la costura desaparece.
- **Los pies de la figura.** 150 px de sprite en 216 de pantalla: por debajo de −33 el borde
  inferior los corta justo en el filo, que se lee como un error y no como que está subiendo.
  Quedó en −28.

### 14.13 🚨 Toda la UI se apagaba de noche junto con el barrio

Encontrado al ver el telón naranja `#EF7250` salir **marrón oscuro**.

El material por defecto de un `SpriteRenderer` en URP 2D es `Sprite-Lit-Default`, así que **la
Global Light 2D multiplica también a la interfaz**. Desde la fase 7, cuando el ciclo de día
empezó a oscurecer, se venían apagando con el barrio: la barra de tiempo, el aro del
skillcheck, su contorno, el velo, la aguja y los carteles. Un color elegido de la paleta
dejaba de ser ese color en pantalla.

**La regla: la UI no es parte del mundo y no la ilumina el sol.** Todo lo que se dibuja para
que el jugador lea —y no para que exista dentro de la calle— usa `Assets/Materials/UI_Unlit.mat`
(`Universal Render Pipeline/2D/Sprite-Unlit-Default`). Medido después del arreglo: **10 de 10
renderers de la capa FX sin iluminar, 0 iluminados.**

⚠️ Esto **no invalida** las mediciones de contraste del contorno del aro (§11.8): se tomaron de
capturas reales con la UI ya oscurecida, o sea en peores condiciones que las de ahora. El
grosor 2 sigue alcanzando, con más margen que el medido. Las de la fuente (§14.9) se hicieron
sobre colores de la paleta, sin pasar por la luz, así que valen tal cual.

### 14.14 El tono de la crucifixión — mirado, no asumido

El pedido era: silueta pura, sin sangre, heridas, corona ni ningún símbolo religioso, y que el
chiste apunte al vendedor pesado y no a la fe. **Mi lectura es que cumple**, y estas son las
razones, no la impresión:

- El arte **no trae un solo marcador religioso**: la cruz es una T lisa —podría ser un poste—,
  la figura está **parada con los brazos abiertos**, no colgando: sin corona, sin heridas, sin
  paño, sin clavos, sin INRI, sin halo.
- **El maletín es lo único que no es silueta** en todo el cuadro: es el único objeto con
  tonos medios, así que es lo primero que se lee. El remate está donde tiene que estar.
- Los cuervos y la loma pelada son iconografía de "se te terminó el día", no de culto.

**Lo que hay que saber para poder cambiar de opinión:** medido, el **34.9%** de la figura
sobresale de la cruz, pero las dos son **exactamente el mismo color** (`#0E0B22`), así que se
funden en una sola masa. En pantalla no se ve una persona en una cruz: se ve **una cruz gruesa**.

Eso es lo que mantiene la lectura del lado del vendedor, y es el único parámetro que la mueve:
**si algún día se separa la figura para que se lea como persona, la imagen pasa a ser una
crucifixión explícita.** Queda anotado para que la decisión sea deliberada y no un efecto
lateral de correr un sprite.

### 14.15 Una trampa más del instrumento

6. **El orden de `ResolveEnding` gana sobre los umbrales sueltos.** Bajar `ascensionAtLeast` a
   0 para ver la ascensión no alcanza: la crucifixión se evalúa PRIMERO, así que con 0
   conversiones seguía saliendo crucifixión. Hay que bajar los dos. No es un bug —el orden es
   correcto— pero es la clase de cosa que se lee como "el asset está mal cargado".

7. 🚨 **La peor de todas: Play Mode corriendo el ASSEMBLY VIEJO.** Un `
` literal metido en
   un tooltip rompió la compilación. Unity dejó entrar a Play igual —o eso pareció— y se
   midió un componente que en ese momento no existía como código. Después, un `using`
   faltante (`RunPhase` vive en `BuenosDias.Simulation`) impidió entrar a Play del todo, y
   `EditorApplication.EnterPlaymode()` **falla en silencio**: el comando devuelve bien y el
   `isPlaying` siguiente da `False`.

   Las otras seis mienten sobre **lo que medís**. Esta miente sobre **QUÉ estás midiendo**, y
   por eso es peor: las conclusiones son coherentes, reproducibles y sobre otro programa.

   → Es de la familia *generador contra artefacto*, más abajo.

   **Reglas que salen de acá:**
   - Después de CADA `AssetDatabase.Refresh`, leer la consola. No alcanza con que el refresh
     devuelva bien.
   - Antes de creerle a una medición en Play Mode, **verificar `EditorApplication.isPlaying`
     en el mismo comando que mide**. Que el juego parezca correr no prueba que tu código esté
     adentro.
   - Ojo con los `
` en tooltips escritos por script: se escriben como salto real y parten
     el string literal.

8. **En Edit Mode, un `MonoBehaviour` sin `[ExecuteAlways]` no corrió su `Awake`.** Medir
   "0 renderers" sobre un `TextLabel` en Edit Mode da 0 también para los que andan perfecto en
   runtime. El control que lo destapó fue comparar contra un label que SÍ funciona: si el
   sano mide igual que el enfermo, el enfermo es la medición.

9. **Cambiar el default de un `[SerializeField]` NO toca lo ya serializado.** El separador del
   HUD se corrigió en el código de `·` a `-` y la escena siguió mostrando el punto medio,
   porque el valor viejo estaba grabado desde que se agregó el componente. Es hermano de la
   trampa 7: creés que cambiaste algo y el archivo tiene guardado lo de antes. **Un default
   solo vale para instancias nuevas**; para las que ya existen hay que tocar el asset.

   → Es de la familia *generador contra artefacto*, más abajo.

10. 🚨 **Una posición calculada RELATIVA a la cámara pero aplicada en el MUNDO.** Los tres
   carteles del HUD salían a la altura correcta —de `orthographicSize`, cada cuadro— pero
   con `transform.position`, mientras la cámara sigue al predicador. Resultado: se quedaban
   clavados donde arrancó la partida y el jugador se alejaba de ellos.

   Costó **cuatro vueltas** porque todos los síntomas apuntaban a otra cosa: se veían bien
   en la selección (la cámara todavía no se movió), los números del diagnóstico daban todos
   correctos, y las letras existían. La barra de tiempo nunca tuvo el problema porque **es
   hija del transform de la cámara** y usa `localPosition`.

   **Regla: todo lo que se ancle a un borde de pantalla cuelga del transform de la cámara y
   se coloca con `localPosition`.** Si un elemento nuevo no lo hace, tiene que gritarlo en
   `Awake` —lo que ahora hace `RunHudView`— porque el síntoma aparece recién al caminar.

   Y la lección de método: cuando el que juega dice "no se ve" y los números dicen "está
   todo bien", el que tiene razón es el que juega. Faltaba preguntar *dónde* lo veía, no
   *si* lo veía.

11. 🚨 **Hijo de la cámara, sí, pero en `localPosition.z = 0`.** Arreglada la trampa 10, el
   HUD seguía sin dibujarse: activo, con letras, en la capa de más arriba, a la altura
   correcta y en el orden correcto. Estaba a distancia CERO de la cámara, y el near clip de
   0,1 lo recortaba entero. La barra de tiempo, que nunca falló, cuelga del mismo transform
   pero en `(0, 0, 5)`.

   **Regla: colgar de la cámara es la mitad del trabajo; la otra mitad es la Z.** Un hijo de
   la cámara necesita `localPosition.z` POSITIVA para quedar delante de ella. Y el síntoma no
   se parece en nada a la causa: "no se dibuja" con todo lo demás correcto se lee como un
   problema de sorting o de material, que es donde se perdieron dos vueltas.

   Y una del método, cara: **el diagnóstico imprimía `transform.position` cuando lo que
   importaba era `localPosition`.** La de mundo trae sumada la posición de la cámara, que se
   mueve, así que un cartel bien puesto a 92 px del centro aparecía como 136 y se leyó dos
   veces como "está fuera de pantalla". Un instrumento que informa el sistema de coordenadas
   equivocado no es menos peligroso que uno que no informa nada.

### 🚨 LAS CUATRO SON LA MISMA: generador contra artefacto

Las trampas 7, 9, 11-bis y la del seeder no son cuatro accidentes: son **cuatro caras de la
misma forma**, y conviene reconocerla antes que memorizar los casos.

En todas hay **dos representaciones de la misma cosa**, una derivada de la otra:

| generador | artefacto | qué pasa cuando no coinciden |
|---|---|---|
| el `.cs` | el assembly compilado | si no compiló, Play corre el VIEJO (trampa 7) |
| el default de un `[SerializeField]` | el valor ya serializado | cambiar el default no toca lo grabado (trampa 9) |
| `GameSceneBuilder` | `Game.unity` | reconstruir apaga las banderas puestas a mano (§15.6) |
| `EndingCatalogSeeder` | los `.asset` de finales | reconstruir borra las piezas agregadas a mano (§14.12) |

**La forma es siempre la misma y falla en las dos direcciones.** Editar el artefacto es
*temporal*: el generador lo pisa la próxima vez que corra. Editar el generador es *inerte*: no
pasa nada hasta regenerar. Y las dos direcciones fallan **sin un solo error**, porque desde el
punto de vista del programa nada está roto: hay un archivo válido, se lee bien, corre bien. Lo
único que está mal es que no es el que vos editaste.

Por eso el síntoma es siempre el mismo y siempre desorienta: **"pero si yo lo cambié"**. Y por eso
las cuatro costaron vueltas enteras: el que busca arranca desconfiando de la lógica, que es
justamente lo único que está bien.

#### Las tres reglas que salen de acá

1. **Verificá sobre el ARTEFACTO, arreglá sobre el GENERADOR.** Mirar el `.asset`, la escena, el
   assembly. Nunca dar por cierto un cambio porque está escrito en el `.cs` o en el builder.
2. **Cuando algo "no tomó el cambio", preguntate quién más escribe ese archivo** antes de
   revisar la lógica. En las cuatro, la lógica estaba bien.
3. **Si un artefacto tiene generador, decilo en el artefacto o en el PLAN.** §14.12 prometía
   "retocar un final es tocar datos" y era falso desde el día que se escribió el seeder. Una
   promesa equivocada en el PLAN es peor que no tener nada escrito, porque se le cree.

#### Y una consecuencia práctica: no bajes umbrales para mirar

La forma de ver la ascensión era bajar `ascensionAtLeast` a mano y acordarse de devolverlo. Eso
es exactamente **la bandera que hay que acordarse de apagar**, o sea la que termina en la build
—§13.8 otra vez— y con el peor castigo posible: una build donde cualquiera asciende y nadie se
entera hasta jugarla.

`GameConfig.forceEnding` la reemplaza. Fuerza el final que se elija **sin tocar un solo umbral**,
viene en `Ninguno` de fábrica y **avisa por consola cada vez que fuerza uno**. Olvidárselo puesto
muestra un final de más y deja el balance intacto: la polaridad correcta.

Medido con el asset real: apagado da `0→Crucifixión, 5→Noche, 10→Ascensión`; forzado a ascensión
da `0→Ascensión, 5→Ascensión`; devuelto a `Ninguno`, los tres umbrales intactos.

---

Y un archivo que apareció sin que nadie lo pidiera: **`ProjectSettings/TimeManager.asset`**.
Tocar `Time.timeScale` lo ensucia y Unity aprovecha para migrarlo a `serializedVersion: 2`,
donde `Fixed Timestep: 0.02` pasa a ser la fracción `2822399/141120000`. El valor es el mismo;
el cambio no es de nadie. Se revirtió.

---

## 15. La tanda de después del playtest

El juego **dejó de ser de un botón**. La arquitectura de entrada está en §13bis.

### 15.1 ✅ Los tres bugs

- **El contador final contaba gente que ya se había ido.** `DayDirector` emitía
  `skillcheck.Converts`, que es monótono y no baja nunca, en vez de `Followers`. Convertir 10 y
  perder 3 daba 10. Los dos números existían desde siempre y nadie los había cruzado. El mismo
  valor alimenta los umbrales de los finales, así que la ascensión se le regalaba a quien llegó
  a 10 y terminó con 7.
  **`Converts` sigue alimentando la rampa de dificultad a propósito**: es "cuánto llevás hecho
  del día" y tiene que ser monótona. Si leyera la comitiva, perder seguidores aflojaría la
  dificultad dos veces, porque el término de comitiva ya la afloja por su lado.
- **`BajarDeTramo` es lo que corre.** Medido contra el asset: 10→7, 6→3, y por debajo de 5 no se
  va nadie. El castigo es **desparejo a propósito**: fallar con 8 cuesta uno y con 7 cuesta
  cuatro. Se deja así —el tope de un tramo tiene que ser una posición precaria— pero **tiene que
  ser visible en el HUD**, y lo es: el `×2` al lado del contador de almas (§15.5.1).
- **La aguja no llegaba a la zona.** Medido: la punta del PNG llegaba a 27,5 px y la zona
  arranca en 46. `ArcPainter.PaintRay` pinta un rayo de ancho CONSTANTE (un sector angular se
  ensancha con el radio: saldría en punta adentro y gordo afuera). Se pinta una vez y la gira el
  transform. Medido después: 60 px, 36 píxeles dentro de la banda 46–55.

### 15.2 🚨 La ocupación se tira UNA SOLA VEZ

Es el cambio de reglas más grande de la tanda.

**Insistir no vuelve a preguntar si hay alguien.** Pregunta si el que está adentro se decide a
abrir. Si cada insistencia volviera a tirar la ocupación, la probabilidad acumulada tendería a 1
y insistir SIEMPRE terminaría funcionando: las señales dejarían de informar un riesgo para
informar un costo de tiempo, y leer la casa pagaría menos en vez de más.

Con una sola tirada aparecen **tres** desenlaces en vez de dos:

| lo que ve el jugador | lo que significa |
|---|---|
| Abren | skillcheck normal |
| **La cortina se movió y no abren** | hay alguien y te está ignorando |
| No pasó nada | la casa está vacía, insistir es tiempo tirado |

El del medio es el estado nuevo. Lo que los separa es el asomo de cortina, que **solo dispara si
hay alguien** — o sea que el sistema de tells que ya existía se vuelve el desempate sin tener que
agregar nada. Insistir deja de ser una apuesta y pasa a ser una lectura.

**Y la pregunta del pity se disuelve sola**: una tirada de ocupación, una decepción. Medido por
qué importaba: con la regla contraria, plantarse en UNA casa y tocar tres veces llevaba el
timbrazo de p=0,450 a **0,890**, dejaba el pity en su techo (0,55) **y armaba la garantía** —o
sea el techo del anti-racha y la casa siguiente asegurada sin caminar un metro. Es el mismo
problema de §10.4 con otra puerta de entrada.

#### Curva medida contra el asset

| timbrazo | abre | espera | zona | acumulado si hay alguien |
|---|---|---|---|---|
| 1 | 0,620 | 1,00 | 1,00 | 0,620 |
| 2 | 0,800 | 0,80 | 0,87 | 0,924 |
| 3 | 0,980 | 0,60 | 0,74 | 0,998 |
| 4 | 1,000 | 0,40 | 0,61 | 1,000 |

⚠️ **Para tunear**: con `openChanceGainPerInsist` en 0,18 la chance llega a 1 en el cuarto
timbrazo, así que la decisión "insisto o me voy" solo tiene filo en los primeros dos o tres. Si
se quiere que la decisión viva más, hay que bajar esa ganancia.

Si la casa está VACÍA, `abre` es 0 en todos los timbrazos y para siempre.

### 15.3 ✅ Cableado de la entrada

`PreacherController`, `ReligionSelector` y `RunDirector` consumen `GameInput`. `OneButtonInput`
queda como el lector CRUDO del botón, que es lo que usa la modalidad de teclado por dentro —así
que el rescate de controles ruidosos y la ventana de diagnóstico (§13.7) siguen sirviendo igual
en las dos modalidades.

El `ReligionSelector` se simplificó de golpe: pasar de religión y confirmar son dos ACCIONES, y
la traba contra confirmar una vez por cuadro y la memoria del apretón en curso se fueron a la
fuente de un botón, que es la única que las necesita.

⚠️ **Cambiar el tipo de un campo serializado rompe la referencia de la escena.** Unity avisa
con *"Serialized reference type mismatch: field 'input' expects 'GameInput'"* y la trata como
null. Hubo que agregar el `GameInput` a mano y recablear los tres. Los builders de
`Assets/Editor/Authoring` también se actualizaron, o una escena reconstruida saldría sin él.

### 15.4 ✅ El felpudo

Siete estados. Los cuatro del medio son "con el pie en la plancha": se entra pisando y se sale
soltando, y bajarse vale en todos menos en `Atendido`.

```
Caminando  --pisa-->  Acercandose  --llega-->  EnElFelpudo  --timbre-->  Esperando
                                                                            |
                        Resuelto  <--resuelve--  Atendido  <--abren--------- +
                            |                                                |
                         (suelta)                            SinRespuesta <--+
                            v                                  |     |
                        Caminando  <---------(suelta)----------+     +--(timbre)--> Esperando
```

**La puntería se congela en el instante en que el pie toca la plancha**, no al llegar a la
puerta. Es toda la mecánica: la precisión sigue siendo del jugador, pero el personaje ya no
queda parado a tres metros hablándole al aire. Si se midiera al llegar, todas las puertas darían
puntería perfecta y el sistema entero se caería. Vive en `DoorApproach`, que es un tipo propio y
no tres campos sueltos del controlador: sueltos, nada impedía leer la puntería mientras el
predicador caminaba por la vereda.

**Frenar mal castiga dos veces**: se pierden segundos caminando hasta la puerta Y queda el
skillcheck más difícil. Puede ser demasiado, así que
`DoorbellConfig.precisionAffectsSkillcheck` apaga la segunda mitad y deja solo el costo en
tiempo. Es la perilla para probar si con un castigo alcanza.

⚠️ Al RESOLVER el skillcheck se lee el estado REAL de la plancha, no el que había al abrirse la
puerta: quien se bajó por reflejo a mitad del check arranca a caminar en ese mismo instante, en
vez de quedar trabado teniendo que volver a subirse solo para poder bajarse.

#### ✅ Geometría medida en Play Mode, contra casas reales

Alcance 2,44u antes de la puerta y 1,44u después —pasarse se castiga— con tolerancia de llegada
de 0,047u.

| frenar en | puntería | va hacia |
|---|---|---|
| −3,0u | 0,000 | ninguna (fuera de alcance: frena y pierde el tiempo) |
| −2,0u | 0,179 | adelante |
| −1,0u | 0,590 | adelante |
| −0,4u | 0,836 | adelante |
| 0,0u | **1,000** | ya llegó |
| +0,4u | 0,722 | **atrás** |
| +1,0u | 0,304 | **atrás** |
| +1,6u | 0,000 | ninguna |

`Acercandose` usa la animación de CAMINAR. Sin esa línea el predicador volvía a deslizarse sin
animación, que es el bug que ya se pagó una vez en la fase 5 y no se vio hasta la 9.

### 15.4b ✅ Las dos de COMPOSICION

Las dos se reportaron por el síntoma, y en las dos el síntoma nombraba una causa que no era.

#### El halo del farol: no era el orden ni el tamaño del lienzo

Las dos sospechas del reporte se descartan **midiendo**:

| sospecha | qué dice la medición |
|---|---|
| "se dibuja delante de la reja" | halo en `HouseDetails/7`, reja en 10, portón en 11. **Ya estaba detrás**: el arreglo de la fase 4 nunca se había caído. |
| "el sprite es enorme" | el lienzo era 60×60 pero **solo 16 px tenían tinta**. |

Lo que estaba mal era la FORMA. Píxel por píxel: alfa **255 hasta el radio 7 y 0 desde el 9**,
con el color constante en (255, 196, 107). O sea un **disco duro y opaco**, no un degradé. Sumado
con `Blend One One` sobre una pared clara, un disco saturado se satura a blanco y deja de leerse
como luz: se lee como una calcomanía pegada al lado de la puerta.

`GlowSpriteFactory` lo regenera con caída cuadrática —núcleo de 2 px a alfa 160, cero a los 11—
sobre un lienzo de 32×32. El shader premultiplica por alfa (`rgb * a * intensidad`), así que
bajar el alfa SÍ apaga la suma; oscurecer el color hacia el negro habría inventado tonos que no
están en la paleta.

⚠️ Lo que hay que recordar del método: **las dos hipótesis del que juega eran razonables y las
dos eran falsas.** Lo que las descartó no fue mirar la pantalla otra vez sino leer los píxeles del
asset. Cuando el síntoma es "se ve mal", el que sabe es el archivo.

#### Los arbustos: estaban apoyados en un borde que no existe

Se sembraban con `groundOffsetPixels = -64`, que es el borde inferior de la caja de **diseño**. El
borde REAL es **−74**: la Pixel Perfect Camera sube el tamaño ortográfico a 3,6875 en runtime
(§3.8) y la ventana muestra 10 px más abajo. Resultado: la mata entera —36 px— visible, más una
franja de calle debajo. Un prop de primer plano visto entero deja de ser marco y pasa a ser un
objeto tirado en el asfalto, que es exactamente como se leía.

Ahora van en **−90**, o sea 16 px por debajo del borde real: entran cortadas por el marco y se ven
unos 18 px, la mitad que antes. Y la separación se duplicó, de 50–160 a **90–240**, que en 384 px
de ancho pasa de 2,4–7,7 matas en pantalla a 1,6–4,3.

⚠️ El −90 está calibrado contra el borde a ortho 3,6875. Si cambia la relación de aspecto, hay que
volver a mirarlo.

### 15.5 🔴 Lo que falta de esta tanda

~~1. HUD~~ ✅ hecho, y **rehecho después de verlo jugando**. La primera versión tenía un cartel
centrado arriba que decía `COMITIVA 7 - ×2 OBJECIONES`: media pantalla para tres datos, dos de
los cuales ya estaban a la vista. Lo único que agregaba de verdad —el tramo, que cambia la
dificultad sin anunciarse— entra en un `×2` chiquito. Quedó así:

| dónde | qué |
|---|---|
| arriba a la izquierda | `ui_icono_alma`, el escudo de 16×16 |
| pegado a su derecha, escala 3 | las almas (`Followers`), tinta oscura con sombra hueso |
| pegado al número, escala 1 | `×2`, el tramo de comitiva |
| centrado arriba | la barra: riel, relleno y `ui_marco_barra` encima |
| puntas de la barra | `ui_icono_sol` a la izquierda, `ui_icono_luna` a la derecha |
| pegado a la luna | los segundos |

El marco mide 128×12 y su hueco 124×8, así que el relleno pasó de 160×6 a **124×8** y se
dibuja DEBAJO del marco: el relleno llena el hueco y el marco le pone el borde. Al revés, con
la barra llena el relleno taparía el borde justo cuando más se mira.

⚠️ **El ícono de almas va a escala 1 y no 3.** La medición manda: `ui_icono_alma` es un ESCUDO
de 16×16 con tinta de borde a borde, no un glifo chiquito. A escala 1 mide 16 px contra los 15
que mide un dígito a escala 3 —o sea que ya pesa lo mismo—; a escala 3 mediría 48 y sería tres
veces el número. `soulsIconScale` está en el Inspector por si el ojo dice otra cosa que la
regla.

Los cuatro comparten una sola altura: almas y segundos se centran contra la barra, y el `×2`
apoya en la MISMA línea base que el número de almas aunque mida un tercio —los dos glifos se
dibujan desde el borde de abajo de su celda, así que compartir la Y ya los alinea como un
renglón—. Antes había tres alturas distintas y se leía como tres cosas sueltas.

`TextLabel` sabe dibujar un **contorno** de 1 o 2 px, medido en píxeles de PANTALLA y no de la
letra: un borde de 2 mide 2 en escala 1 y en escala 3. Cuesta una copia entera de la palabra por
cada corrimiento —8 con grosor 1, **24** con grosor 2—.

**Hoy no lo usa nadie, y está bien así.** El contador lo tuvo mientras fue el único que se
bancaba caer sobre cualquier fondo. Con el escudo al lado, la legibilidad de esa esquina la
sostiene el ícono —que trae su propio contorno de 1 px, dibujado, gratis— y el número se arregla
con tinta oscura y sombra hueso: **una** copia en vez de veinticuatro por cuadro. La capacidad
queda para el día que un cartel tenga que leerse sobre algo impredecible.

⚠️ **Los dos ejes del HUD no se anclan igual, y es a propósito.** El alto sale del
`orthographicSize` crudo, porque tiene que quedar alineado con la barra de tiempo, que se ancla
así. El ancho se recorta contra la caja de diseño de 384: sin eso, en una ventana más ancha el
contador se va a buscar el borde físico y aparece despegado de todo lo demás.

Para cuando vuelva a hacer falta un separador: el `·` **no existe en la fuente**; el guion sí.

~~2. §4.3 el naranja~~ ✅ hecho. El techo pasó de absoluto a POR ESLABÓN, porque el absoluto
borraba la diferencia entre naranja y verde justo con la cadena más larga. Con base 3,
perfecto 4, bueno 1,5 y techo 7/eslabón, la puerta máxima devuelve 28 s de 72.
⚠ **El techo tiene que quedar en base + perfecto o por encima**, o satura las dos calidades y
vuelve el bug.

~~3. §4.2 — caminar más rápido con el progreso del día~~ ✅ hecho.

La curva `speedByDayProgress` vive en `WalkConfig` y no en un componente nuevo. **La curva es
balance, no responsabilidad**: el controlador ya le preguntaba `UnitsPerSecondFor(religion)`, y
sumarle el avance del día es un parámetro más en la misma pregunta. La alternativa era sacar la
caminata a un `PreacherWalk` propio, y eso habría sido partir una FSM verificada para ganar
veinte líneas.

#### La hitbox escala con una división, no con un segundo número

⚠️ **`DoorApproach.At` DIVIDE la distancia por el ritmo antes de preguntar.** Con eso escala la
hitbox entera —el alcance y la curva de puntería— con el mismo número que escala la velocidad.
No hay dos ajustes que puedan quedar desincronizados porque no hay dos ajustes.

Dicho de otra manera: dividida por el ritmo, la distancia deja de ser distancia y pasa a ser
**tiempo hasta la puerta**, que es lo que el jugador está midiendo con el pie.

#### Medido contra el asset

| progreso | ritmo | px/s | alcance antes | alcance después |
|---|---|---|---|---|
| 0,00 | 1,000 | 152,0 | 2,43u | 1,43u |
| 0,25 | 1,055 | 160,3 | 2,57u | 1,51u |
| 0,50 | 1,175 | 178,6 | 2,86u | 1,68u |
| 0,75 | 1,295 | 196,9 | 3,15u | 1,86u |
| 1,00 | 1,350 | 205,2 | 3,29u | 1,94u |

Y la prueba de que el diseño se cumple, que es lo único que importa acá:

| progreso | ventana de frenado |
|---|---|
| 0,0 | **0,513 s** |
| 0,5 | **0,512 s** |
| 1,0 | **0,513 s** |

Constante de punta a punta. Lo único que cambia es el ritmo.

#### Los dos detalles que no se ven en la tabla

- **El viaje hasta la puerta también acelera** (`Approach` usa la velocidad con ritmo). Sin eso,
  frenar mal costaría cada vez MÁS segundos a medida que avanza el día, que es un castigo que
  nadie diseñó.
- **El indicador de timbre lee `PreacherController.Pace`** en vez de recalcularlo. Si lo
  calculara por su cuenta, alcanzaría con que a alguien se le pase una llamada para que el
  cartelito prometa una puerta que el felpudo no agarra.

⚠️ `WalkConfig.PaceAt` tiene un piso de 0,25. Es por una razón boba y cara: una curva que alguien
deje en 0 dejaría al predicador clavado en la vereda **sin ningún error en la consola**, y eso se
lee como que el juego se colgó.

~~4. §4.4 — la ascensión escalonada~~ ✅ hecho.

Faltaban tres cosas y ninguna era un final nuevo: era el mismo final, moviéndose.

#### Las piezas de una cinemática ahora pueden moverse

`EndingLayer` ganó dos campos, y con eso el sistema de finales pasó de láminas a cinemática sin
una sola clase nueva:

| campo | qué hace | quién lo usa hoy |
|---|---|---|
| `driftPixelsPerSecond` | se corre desde donde apareció | la silueta (sube 7 px/s) y los cuatro folletos |
| `degreesPerSecond` | gira | los rayos, a 14°/s |

⚠️ El corrimiento se cuenta desde que la pieza **entra**, no desde que arranca el final. Contado
desde el principio, una pieza que aparece tarde entraría ya corrida: o sea de la nada y en el
aire.

⚠️ **Los rayos son la única pieza del juego que rota, y es una excepción con motivo.** Rotar
pixel art rompe la grilla, pero un abanico radial de 256×256 con pivot al centro no tiene una
sola línea recta horizontal ni vertical que se pueda escalonar. En cualquier otra pieza se
notaría; acá no hay grilla que romper. El tooltip lo dice para que nadie lo copie a un cartel.

Los folletos giran sin rotar nada: los cuatro PNG `ui_folleto_00/22/45/67` son la misma hoja en
cuatro inclinaciones, así que el giro es una animación de cuadros. Ya viene dibujado, y dibujado
no rompe la grilla. Los cuatro salen con rumbo, velocidad, momento y cuadro inicial distintos:
cuatro folletos parejos se leerían como una sola cosa repetida cuatro veces.

#### La fila que sube: `AscensionLine`

Sube la cantidad de almas que el jugador juntó **de verdad** —el número viene en
`RunDirector.EndingReached`— con `staggerSeconds` de retardo entre puesto y puesto. El escalonado
es TODO el efecto: con 0 sube un bloque, y un bloque que sube se lee como un error de dibujo.

Y va de adelante hacia atrás, no al revés. La fila se vacía en la misma dirección en la que venía
caminando, y eso se lee como que se los está llevando a todos; al revés se leería como que los de
atrás se escapan.

⚠️ **No toca a los seguidores de verdad.** El primer intento metió la ascensión adentro de
`FollowerParade` y la dejó en **212 líneas**. El límite hizo su trabajo: esa clase ya hacía tres
cosas —seguir el recorrido, manejar el pool, animar las bajas— y la cuarta no entraba. Y al
mirarlo de nuevo, tampoco correspondía: la ascensión tapa el mundo con un telón, así que los
seguidores de verdad no se verían igual, y meterles un segundo dueño a esos transforms era
pelearse por ellos. La cinemática dibuja los suyos, en su capa, por delante del telón.

#### De paso, dos builders que también pasaron de 200

`RunStateBuilder` llegó a 202 al sumarle el HUD y la fila. El corte no fue por el largo sino por
dónde estaba la costura: los finales son otra pantalla, con su propia capa de dibujo y su propio
catálogo, así que salieron a `EndingRigBuilder`. Y la receta de armar un cartel —fuente, material
sin iluminar, capa, orden— la querían tres builders distintos, así que salió a `LabelFactory`.

### 15.6 ✅ Lo que salió del segundo playtest

#### La puerta no se abría — y no era de insistir

El bug se reportó como "la puerta no se abre cuando insistís y te atienden", pero medido en el
código **no se abría NUNCA**: `HouseInstance` no tenía ninguna forma de abrirse. Insistir solo lo
hacía evidente, porque ahí el jugador paga segundos por una puerta que después se ve igual que la
que no le abrió, y la conclusión razonable es que insistir está roto.

`env_puerta_abierta` (32×56, interior oscuro) estaba en el repo **desde la primera tanda de arte**
y nadie lo había enganchado. Ahora `HouseInstance.SetDoorOpen` cambia el sprite de la hoja —el
hijo `DoorSprite`, no la puerta entera, de la que también cuelgan el timbre y el portón— y
`DoorOpeningView` la abre mientras dura `Atendido`.

⚠️ **La vista se acuerda de qué casa abrió en vez de volver a preguntárselo a la FSM.**
`PreacherController.Abandon` limpia el intento ANTES de avisar el cambio de estado, así que en el
cuadro en que hay que cerrar, `Target` ya es null y la puerta se quedaría abierta para siempre.

⚠️ **`Apply` cierra la puerta.** La casa vive en un pool: una hoja abierta olvidada reaparece
treinta metros más adelante como una casa que abre sola, y el síntoma estaría lejísimos del bug.

#### El brazo no se quedaba estirado

Confirmado: **mantener el timbre no hace nada mecánico**. Se lee como pulso para tocar y para
insistir, en las tres modalidades. Lo único roto era la vista: el timbrazo es una POSE de 0,25 s
que se vencía sola, y eso se leía como que el juego había soltado el botón por su cuenta.

Para arreglarlo cambió lo que significa `IInputSource.Held`: pasó de "el felpudo está pisado" a
**"el botón que AHORA significa esa acción está apretado"**. No cambia ninguna regla —nadie
consulta `Held(Timbre)` para decidir nada— pero deja que la vista sepa lo que la mano está
haciendo.

⚠️ En la modalidad de un botón, `Held(Timbre)` se apaga en cuanto el mantenido se completa,
porque ahí sostener pasa a significar BAJARSE. Es lo correcto: el brazo no tiene que mentir sobre
eso.

⚠️ `PreacherView.Ringing()` exige estado de puerta además del botón. En la modalidad de dos
botones, A significa timbre también mientras se camina, y sin esa condición el predicador cruzaría
la vereda con el brazo estirado tocándole el timbre al aire.

#### El indicador de "acá podés tocar"

Un chevrón que aparece sobre la puerta mientras el predicador camina dentro del alcance. Hacía
falta porque **el alcance es invisible**: frenar bien es toda la mecánica del felpudo, pero hasta
que el pie toca la plancha no hay un solo píxel que diga si estás en zona. Se aprendía a fuerza de
frenar de más y perder segundos, o sea por castigo.

⚠️ **No reimplementa el alcance.** Pregunta con el MISMO `DoorApproach.At` que usa la FSM para
decidir a qué puerta te lleva pisar la plancha, así que el indicador no puede prometer una cosa y
el juego hacer otra. El sprite se genera por código: son veinte píxeles y tres colores.

#### ✅ Deuda encontrada de paso: `GameSceneBuilder` no reconstruía la escena entera

`RunHudView` **no lo armaba ningún builder** —se había agregado a mano— así que correr
*Fase 1 · Construir escena de juego* devolvía una partida sin contador, sin reloj en número y sin
multiplicador. Y no habría fallado nada: simplemente no se veían. Es la peor forma de deuda,
porque el que la encuentra dentro de dos meses no tiene ningún motivo para sospechar del builder.

Cerrada: `RunStateBuilder.BuildHud` arma el HUD entero —los tres carteles, el ícono, el anclaje a
la cámara con Z positiva— y `GameSceneBuilder` lo llama. Lo demás de esta tanda también quedó en
los builders: `DoorOpeningView` y el `input` de `PreacherView` en `RunStateBuilder`, el indicador
de timbre y el marco de la barra en `RunRigBuilder`, la hoja de la puerta en `HousePrefabWiring`.

⚠️ Lo que hay que sostener: **cada componente nuevo que se cuelgue a mano en la escena tiene que
entrar al builder en el mismo commit.** No hay nada que avise si no se hace.

#### ✅ Verificado: se reconstruyó la escena de verdad

No alcanzaba con que compilara. Se corrió *Fase 1* sobre la escena real y se comparó contra la
que había, dos veces:

- **Objetos**: los 90 nombres de la escena reconstruida son exactamente los mismos. Ni uno de
  menos ni uno de más.
- **Escalares serializados**: se extrajeron todos los `campo: valor` de las dos versiones —sin
  `fileID`, que se regeneran— y se compararon.

Y ahí apareció lo que la comparación de objetos no podía ver.

#### 🚨 Reconstruir la escena APAGA las banderas puestas a mano, y no falla nada

La reconstrucción se llevó puestas dos cosas, las dos en silencio y las dos sin romper nada:

| bandera | estaba | quedó |
|---|---|---|
| `GameInput.mode` | `TecladoDosBotones` | `Teclado` |
| `OneButtonInput.acceptNoisyControls` (§13.8) | prendida | apagada |

Ninguna hace fallar el juego: **arranca igual, con otro esquema de control.** El que reconstruye
se entera cuando va a jugar y el botón hace otra cosa, y para entonces ya no tiene motivo para
sospechar del builder.

→ Es de la familia *generador contra artefacto* (§14).

No se hornean los valores "correctos" en el builder a propósito: **no son del builder, son de
quien está jugando ese día.** Lo que sí es del builder es DECIR con qué quedaron, así que el
reporte de *Fase 1* ahora termina con las dos banderas y su valor:

```
  ⚠ Banderas que volvieron a fábrica. Si alguna no es la
    que querías, hay que tildarla de nuevo a mano:
      Modalidad de input.... Teclado
      acceptNoisyControls... False
```

El aviso se probó reconstruyendo otra vez y leyendo la salida, no confiando en que compilara. Es
la misma regla de siempre: **una guarda sin verificar falla igual que no tenerla.**

⚠️ Y §13.8 sigue pendiente: `acceptNoisyControls` quedó PRENDIDA, que es lo que se pidió para la
prueba del gabinete. Cuando esa prueba termine hay que apagarla.
