"""Arnés para probar el firmware del felpudo (la balanza) en la PC, sin placa.

Corre el code.py de verdad con CPython. Antes de cargarlo le cambia board,
digitalio, usb_hid, supervisor, adafruit_hid, adafruit_hx711 y time por
falsos: un reloj simulado, una celda de carga modelada con lo que se midió en
el felpudo real y un teclado que anota qué teclas le llegaron a la PC.

Solo LEE el archivo del firmware. No toca F:\\ ni el puerto serie.

Modelo de la celda (medido en el felpudo el 18/10):
- Crudo en reposo entre +620 000 y +880 000; baja solo (deriva) a 0,3–2 kg/s.
- Ruido por muestra ~0,5 kg. Dientes de sierra: el crudo salta +40 kg de golpe.
- 6400 unidades por kg; el crudo BAJA cuando sube alguien.
- La librería adafruit_hx711 1.1.2 devuelve los negativos como positivos de
  ~4.290.000.000 (extiende el signo con OR y no resta).
- read() de la librería hace dos conversiones; _read_channel_raw() una.
"""

from __future__ import annotations

import bisect
import importlib.util
import itertools
import math
import os
import random
import statistics
import sys
import tempfile
import types

UNIDADES_POR_KG = 6400       # calibración física de la celda, no la del firmware
VACIO, OCUPADO = 0, 1

_FELPUDO = os.path.join(tempfile.gettempdir(), "claude", "F--",
                        "60276189-3375-4566-8488-dc3fa82a2163", "scratchpad", "felpudo")


def ruta_v17():
    return os.environ.get("FELPUDO_CODE") or os.path.join(_FELPUDO, "code_felpudo_v17.py")


def ruta_v16():
    return os.environ.get("FELPUDO_V16") or os.path.join(_FELPUDO, "code_felpudo_v16.py")


class FinDeSimulacion(BaseException):
    """BaseException a propósito: el loop del firmware atrapa Exception."""


# ─────────────────────────── teclas ───────────────────────────

class Keycode:
    """Códigos HID reales (los mismos que adafruit_hid.keycode)."""
    ENTER, ESCAPE, BACKSPACE, TAB, SPACE = 0x28, 0x29, 0x2A, 0x2B, 0x2C


for _i, _c in enumerate("ABCDEFGHIJKLMNOPQRSTUVWXYZ"):
    setattr(Keycode, _c, 0x04 + _i)
for _i, _c in enumerate(["ONE", "TWO", "THREE", "FOUR", "FIVE",
                         "SIX", "SEVEN", "EIGHT", "NINE", "ZERO"]):
    setattr(Keycode, _c, 0x1E + _i)

NOMBRE_TECLA = {v: k for k, v in vars(Keycode).items() if k.isupper()}


def como_devuelve_la_lib(v):
    """El bug de signo de adafruit_hx711: un negativo de 24 bits vuelve como
    un positivo de ~4.290.000.000 en vez de un int negativo."""
    u = v & 0xFFFFFF
    if u & 0x800000:
        u |= 0xFF000000
    return u


# ─────────────────────────── escena ───────────────────────────

class Deriva:
    """Velocidad de deriva que cambia de a poco (tramos lineales), en kg de
    crudo por segundo. Negativo = el crudo baja (lo que se ve en el felpudo)."""

    def __init__(self, rng, duracion_ms, minimo, maximo, tramo_s=(5, 20)):
        self.t = [0.0]
        self.v = [rng.uniform(minimo, maximo)]
        while self.t[-1] < duracion_ms:
            self.t.append(self.t[-1] + rng.uniform(*tramo_s) * 1000)
            self.v.append(rng.uniform(minimo, maximo))
        self.acum = [0.0]
        for i in range(1, len(self.t)):
            dt = (self.t[i] - self.t[i - 1]) / 1000
            self.acum.append(self.acum[-1] + (self.v[i - 1] + self.v[i]) / 2 * dt)

    def kg(self, t):
        i = bisect.bisect_right(self.t, t) - 1
        if i < 0:
            return 0.0
        if i >= len(self.t) - 1:
            return self.acum[-1] + self.v[-1] * (t - self.t[-1]) / 1000
        dt = (t - self.t[i]) / 1000
        a = (self.v[i + 1] - self.v[i]) / ((self.t[i + 1] - self.t[i]) / 1000)
        return self.acum[i] + self.v[i] * dt + a * dt * dt / 2


class Visita:
    """Una persona: rampa al subir, golpe que se apaga en ~300 ms, vaivén
    mientras está parada y rampa al bajar. Tiempos en ms, peso en kg."""

    def __init__(self, t_sube, quieto_ms, kg, rampa_sube, rampa_baja,
                 sobrepico, vaiven_kg, vaiven_hz, fase):
        self.kg = kg
        self.t_sube = t_sube
        self.t_quieto = t_sube + rampa_sube
        self.t_baja = self.t_quieto + quieto_ms
        self.t_abajo = self.t_baja + rampa_baja
        self.sobrepico = sobrepico
        self.vaiven_kg = vaiven_kg
        self.vaiven_hz = vaiven_hz
        self.fase = fase

    def peso(self, t):
        if t < self.t_sube or t >= self.t_abajo:
            return 0.0
        if t < self.t_quieto:
            return self.kg * (t - self.t_sube) / (self.t_quieto - self.t_sube)
        lleno = self.kg + self.vaiven_kg * math.sin(
            2 * math.pi * self.vaiven_hz * t / 1000 + self.fase)
        if t < self.t_baja:
            return lleno + self.kg * self.sobrepico * math.exp(-(t - self.t_quieto) / 100)
        return lleno * (self.t_abajo - t) / (self.t_abajo - self.t_baja)


class Escena:
    """Lo que pasa arriba del felpudo y en la celda. La verdad (quién está
    arriba) sale de las visitas; los saltos, corrimientos y glitches son
    artefactos del sensor que NO cambian la verdad."""

    def __init__(self, semilla=0, duracion_s=60.0, crudo_inicial=None,
                 deriva_kg_s=(-2.1, 0.3), ruido_kg=0.6):
        self.semilla = semilla
        self.rng = random.Random(semilla)
        self.duracion_ms = duracion_s * 1000
        self.crudo_inicial = (self.rng.uniform(620_000, 880_000)
                              if crudo_inicial is None else crudo_inicial)
        self.deriva = (Deriva(self.rng, self.duracion_ms + 60_000, *deriva_kg_s)
                       if deriva_kg_s else None)
        self.ruido_kg = ruido_kg
        self._rng_ruido = random.Random(semilla * 7919 + 17)
        self._ruido = []
        self.visitas = []
        self.saltos = []        # (t_ms, kg de crudo): + = el crudo sube (menos peso)
        self.corrimientos = []  # (t_ms, dur_ms, kg de crudo)
        self.glitches = []      # (t_ms, tipo, valor, cuantas)

    # ── armado ──

    def agregar_visita(self, t_s, quieto_s, kg=None, rampa_ms=None, sobrepico=None,
                       vaiven_kg=None):
        r = self.rng
        v = Visita(
            t_sube=t_s * 1000,
            quieto_ms=quieto_s * 1000,
            kg=r.uniform(50, 120) if kg is None else kg,
            rampa_sube=r.uniform(100, 300) if rampa_ms is None else rampa_ms,
            rampa_baja=r.uniform(100, 300) if rampa_ms is None else rampa_ms,
            sobrepico=r.uniform(0, 0.3) if sobrepico is None else sobrepico,
            vaiven_kg=r.uniform(0.5, 2.0) if vaiven_kg is None else vaiven_kg,
            vaiven_hz=r.uniform(0.3, 1.0),
            fase=r.uniform(0, 2 * math.pi),
        )
        self.visitas.append(v)
        self.visitas.sort(key=lambda x: x.t_sube)
        return v

    def agregar_visitas(self, desde_s, hasta_s, quieto_s=(2, 20), pausa_s=(3, 10),
                        kg=(50, 120)):
        """Visitas de a una, separadas por pausas, hasta hasta_s."""
        t = desde_s
        while True:
            quieto = self.rng.uniform(*quieto_s)
            if t + quieto + 1 > hasta_s:
                return
            v = self.agregar_visita(t, quieto, kg=self.rng.uniform(*kg))
            t = v.t_abajo / 1000 + self.rng.uniform(*pausa_s)

    def agregar_salto(self, t_s, kg_crudo):
        self.saltos.append((t_s * 1000, kg_crudo))
        self.saltos.sort()

    def agregar_sierra(self, desde_s, hasta_s, cada_s=(15, 40), kg=(40,)):
        """Dientes de sierra: el crudo salta de golpe cada tanto y se queda ahí."""
        t = desde_s + self.rng.uniform(*cada_s) / 2
        while t < hasta_s:
            self.agregar_salto(t, self.rng.choice(kg))
            t += self.rng.uniform(*cada_s)

    def agregar_corrimiento(self, t_s, kg_crudo, dur_s=0.0):
        self.corrimientos.append((t_s * 1000, max(dur_s * 1000, 1e-6), kg_crudo))

    def agregar_glitch(self, t_s, tipo, valor, cuantas=1):
        """tipo: "pico" (suma valor kg de crudo), "bit" (invierte el bit valor
        de los 24) o "fijo" (la conversión vale valor). cuantas: muestras seguidas."""
        self.glitches.append((t_s * 1000, tipo, valor, cuantas))

    # ── señal ──

    def ruido(self, k):
        k = max(k, 0)
        while len(self._ruido) <= k:
            self._ruido.append(self._rng_ruido.gauss(0, self.ruido_kg) if self.ruido_kg else 0.0)
        return self._ruido[k]

    def peso(self, t):
        return sum(v.peso(t) for v in self.visitas if v.t_sube <= t < v.t_abajo)

    def crudo(self, k, t):
        """Crudo real (con signo) de la conversión k, que termina en t ms."""
        kg = self.deriva.kg(t) if self.deriva else 0.0
        for ts, salto in self.saltos:
            if ts > t:
                break
            kg += salto
        for ts, dur, corr in self.corrimientos:
            if t >= ts:
                kg += corr * min(1.0, (t - ts) / dur)
        kg -= self.peso(t)
        kg += self.ruido(k)
        return self.crudo_inicial + kg * UNIDADES_POR_KG

    # ── verdad ──

    def arriba(self, t):
        return sum(1 for v in self.visitas if v.t_sube <= t < v.t_baja)

    def transiciones(self):
        """[(t_ms, OCUPADO/VACIO)]: cuándo el felpudo pasa de vacío a ocupado
        (empieza a subir el primero) y de ocupado a vacío (empieza a bajar el último)."""
        eventos = sorted([(v.t_sube, 1) for v in self.visitas] +
                         [(v.t_baja, -1) for v in self.visitas])
        n, salida = 0, []
        for t, d in eventos:
            antes, n = n, n + d
            if antes == 0 and n > 0:
                salida.append((t, OCUPADO))
            elif antes > 0 and n == 0:
                salida.append((t, VACIO))
        return salida


# ─────────────────────────── falsos ───────────────────────────

class Reloj:
    def __init__(self, t0_ms=0.0):
        self.ns = int(t0_ms * 1_000_000)
        self.fin_ns = None
        self.puede_terminar = lambda: True

    def avanzar_a_ns(self, ns):
        if ns > self.ns:
            self.ns = ns

    def chequear_fin(self):
        # No corta con una tecla apretada (salvo que el USB no vuelva nunca).
        if self.fin_ns is not None and self.ns >= self.fin_ns and (
                self.puede_terminar() or self.ns >= self.fin_ns + 5_000_000_000):
            raise FinDeSimulacion()

    def modulo_time(self):
        m = types.ModuleType("time")
        m.monotonic_ns = lambda: self.ns
        m.monotonic = lambda: self.ns / 1e9

        def sleep(segundos):
            self.ns += int(round(segundos * 1e9))
            self.chequear_fin()

        m.sleep = sleep
        return m


class Celda:
    """El HX711 con su celda: conversiones en una grilla fija (sps), un dato
    nuevo por conversión, y se pierde si no se lee antes de la siguiente."""

    def __init__(self, sim, sps, fase_ms):
        self.sim = sim
        self.reloj = sim.reloj
        self.escena = sim.escena
        self.periodo_ns = 1e9 / sps
        self.fase_ns = fase_ms * 1e6
        self.asentamiento_ns = (400 if sps < 40 else 50) * 1e6   # datasheet, tras reset
        self.ultima_leida = -1
        self.sin_datos = [(a * 1e9, b * 1e9) for a, b in sim.hx_cortes_s]
        self.fallas = sorted(t * 1e9 for t in sim.hx_fallas_s)
        self.lecturas = 0
        self.reinicios = []
        self._mods = {}
        for t, tipo, valor, cuantas in self.escena.glitches:
            k0 = math.ceil((t * 1e6 - self.fase_ns) / self.periodo_ns)
            for j in range(cuantas):
                self._mods.setdefault(k0 + j, []).append((tipo, valor))

    def _rel_ns(self):
        return self.reloj.ns - self.sim.t0_ns

    def _k(self, ns):
        return math.floor((ns - self.fase_ns) / self.periodo_ns)

    def _t(self, k):
        return self.fase_ns + k * self.periodo_ns

    def _existe(self, k):
        t = self._t(k)
        return k >= 0 and not any(a <= t < b for a, b in self.sin_datos)

    def hay_dato(self):
        k = self._k(self._rel_ns())
        return k > self.ultima_leida and self._existe(k)

    def valor(self, k):
        v = int(round(self.escena.crudo(k, self._t(k) / 1e6)))
        bits = []
        for tipo, x in self._mods.get(k, ()):
            if tipo == "pico":
                v += int(round(x * UNIDADES_POR_KG))
            elif tipo == "fijo":
                v = int(x)
            elif tipo == "bit":
                bits.append(x)
        v = max(-0x800000, min(0x7FFFFF, v))          # el ADC satura
        for b in bits:
            u = (v & 0xFFFFFF) ^ (1 << b)
            v = u - 0x1000000 if u & 0x800000 else u
        return v

    def leer(self):
        """Lo que hace _read_channel_raw(): espera dato (while is_busy: pass),
        lo consume y lo devuelve con el bug de signo de la librería."""
        if self.fallas and self._rel_ns() >= self.fallas[0]:
            self.fallas.pop(0)
            raise OSError("HX711: lectura fallida (simulada)")
        while not self.hay_dato():
            k = max(self._k(self._rel_ns()), self.ultima_leida) + 1
            while not self._existe(k):
                k += 1
            self.reloj.avanzar_a_ns(self.sim.t0_ns + int(math.ceil(self._t(k))))
            self.reloj.chequear_fin()
        k = self._k(self._rel_ns())
        self.ultima_leida = k
        self.lecturas += 1
        return como_devuelve_la_lib(self.valor(k))

    def reiniciar(self):
        t = self._rel_ns()
        self.reinicios.append(t / 1e6)
        self.sin_datos.append((t, t + self.asentamiento_ns))


class _Pin:
    def __init__(self, nombre):
        self.nombre = nombre


class _Direction:
    INPUT = "input"
    OUTPUT = "output"


class _Pull:
    UP = "up"
    DOWN = "down"


class _DigitalInOut:
    def __init__(self, pin):
        self.pin = pin
        self.direction = _Direction.INPUT
        self.value = False
        self.pull = None

    def deinit(self):
        pass


class _HX711Falso:
    CHAN_A_GAIN_128 = 25
    CHAN_A_GAIN_64 = 27
    CHAN_B_GAIN_32 = 26
    _sim = None

    def __init__(self, data_pin=None, clock_pin=None, *args, **kwargs):
        self._celda = self._sim.celda

    @property
    def is_busy(self):
        return not self._celda.hay_dato()

    def _read_channel_raw(self, chan_gain):
        return self._celda.leer()

    def _read_channel(self, chan_gain):
        return self._read_channel_raw(chan_gain)      # tara 0

    def read_channel_blocking(self, chan_gain):
        self._read_channel_raw(chan_gain)             # la lib descarta la primera
        return self._read_channel(chan_gain)

    def read(self, chan_gain=25):
        return self.read_channel_blocking(chan_gain)

    def _initialize(self):
        self._celda.reiniciar()


class _AnalogIn:
    def __init__(self, hx, chan_gain):
        self._hx = hx
        self._chan = chan_gain

    @property
    def value(self):
        return self._hx.read(self._chan)


class _TecladoFalso:
    LED_NUM_LOCK = 0x01
    LED_CAPS_LOCK = 0x02
    LED_SCROLL_LOCK = 0x04
    LED_COMPOSE = 0x08
    _sim = None

    def __init__(self, devices=None, timeout=None):
        self._local = set()
        self._sim.teclado = self

    def _enviar(self):
        if self._sim.usb_cortado():
            raise OSError(5, "USB caído (simulado)")
        self._sim.pc_recibe(set(self._local))

    def press(self, *keycodes):
        self._local.update(keycodes)
        self._enviar()

    def release(self, *keycodes):
        self._local.difference_update(keycodes)
        self._enviar()

    def release_all(self):
        self._local.clear()
        self._enviar()

    def send(self, *keycodes):
        self.press(*keycodes)
        self.release_all()

    def led_on(self, led_code):
        return bool(self._sim.leds() & led_code)

    @property
    def led_status(self):
        return bytes([self._sim.leds()])


class _Runtime:
    def __init__(self, sim):
        self._sim = sim
        self.autoreload = True
        self.serial_connected = False

    @property
    def usb_connected(self):
        return True


# ─────────────────────────── simulación ───────────────────────

_NADA = object()
_contador = itertools.count()


class Simulacion:
    """Una corrida del firmware contra una Escena.

    sps: conversiones por segundo del HX711 (10.6 de fábrica, 80 con el jumper).
    t0_ms: dónde arranca el reloj (para simular días prendido).
    numlock_*: estado de Bloq Num en la PC; la PC lo informa recién a los
        numlock_reporta_desde_s de conectar (antes el LED se lee apagado).
    usb_cortes_s: [(desde, hasta)] en que mandar un reporte HID tira OSError.
    hx_cortes_s: [(desde, hasta)] en que el HX711 no entrega datos.
    hx_fallas_s: instantes en que una lectura del HX711 tira OSError.
    """

    def __init__(self, escena, ruta=None, *, sps=10.6, t0_ms=0.0, fase_ms=None,
                 numlock_inicial=False, numlock_toques_s=(), numlock_reporta_desde_s=1.0,
                 usb_cortes_s=(), hx_cortes_s=(), hx_fallas_s=()):
        self.escena = escena
        self.ruta = ruta or ruta_v17()
        self.t0_ns = int(t0_ms * 1_000_000)
        self.reloj = Reloj(t0_ms)
        self.numlock_inicial = numlock_inicial
        self.numlock_toques_ms = sorted(t * 1000 for t in numlock_toques_s)
        self.numlock_desde_ms = numlock_reporta_desde_s * 1000
        self.usb_cortes_ms = [(a * 1000, b * 1000) for a, b in usb_cortes_s]
        self.hx_cortes_s = list(hx_cortes_s)
        self.hx_fallas_s = list(hx_fallas_s)
        if fase_ms is None:
            fase_ms = random.Random(escena.semilla + 99991).uniform(0, 1000 / sps)
        self.celda = Celda(self, sps, fase_ms)
        self.teclado = None
        self.teclas = []          # (t_ms, keycode) cuando a la PC le llega la tecla apretada
        self.retenciones = []     # (keycode, ms que la PC la vio apretada)
        self.pc_apretadas = set()
        self._desde = {}
        self.salida = []          # (t_ms, texto) de cada print del firmware
        self.reloj.puede_terminar = lambda: not self.pc_apretadas
        self.mod = None
        with open(self.ruta, encoding="utf-8") as f:
            fuente = f.read()
        # v17 en adelante: todo el hardware dentro de main(), protegido por __main__.
        # v16 y anteriores: loop infinito a nivel de módulo.
        self.legacy = not ("def main(" in fuente and "__main__" in fuente)

    def ahora_ms(self):
        return (self.reloj.ns - self.t0_ns) / 1e6

    def usb_cortado(self):
        t = self.ahora_ms()
        return any(a <= t < b for a, b in self.usb_cortes_ms)

    def leds(self):
        t = self.ahora_ms()
        if t < self.numlock_desde_ms:
            return 0
        toques = bisect.bisect_right(self.numlock_toques_ms, t)
        return 0x01 if self.numlock_inicial ^ (toques % 2 == 1) else 0

    def pc_recibe(self, apretadas):
        t = self.ahora_ms()
        for k in apretadas - self.pc_apretadas:
            self.teclas.append((t, k))
            self._desde[k] = t
        for k in self.pc_apretadas - apretadas:
            self.retenciones.append((k, t - self._desde.pop(k)))
        self.pc_apretadas = apretadas

    def _print(self, *args, sep=" ", end="\n", **kwargs):
        self.salida.append((self.ahora_ms(), sep.join(str(a) for a in args)))

    def _falsos(self):
        sim = self

        class HX711(_HX711Falso):
            _sim = sim

        class Keyboard(_TecladoFalso):
            _sim = sim

        def modulo(nombre, **attrs):
            m = types.ModuleType(nombre)
            m.__dict__.update(attrs)
            return m

        board = modulo("board")
        board.__dict__["__getattr__"] = lambda nombre: _Pin(nombre)
        hid = modulo("adafruit_hid")
        hid_kb = modulo("adafruit_hid.keyboard", Keyboard=Keyboard)
        hid_kc = modulo("adafruit_hid.keycode", Keycode=Keycode)
        hid.keyboard, hid.keycode = hid_kb, hid_kc
        hx = modulo("adafruit_hx711")
        hx_hx = modulo("adafruit_hx711.hx711", HX711=HX711)
        hx_ai = modulo("adafruit_hx711.analog_in", AnalogIn=_AnalogIn)
        hx.hx711, hx.analog_in = hx_hx, hx_ai
        return {
            "time": self.reloj.modulo_time(),
            "board": board,
            "digitalio": modulo("digitalio", DigitalInOut=_DigitalInOut,
                                Direction=_Direction, Pull=_Pull),
            "supervisor": modulo("supervisor", runtime=_Runtime(self)),
            "usb_hid": modulo("usb_hid", devices=[object()]),
            "adafruit_hid": hid,
            "adafruit_hid.keyboard": hid_kb,
            "adafruit_hid.keycode": hid_kc,
            "adafruit_hx711": hx,
            "adafruit_hx711.hx711": hx_hx,
            "adafruit_hx711.analog_in": hx_ai,
            "neopixel": None,          # import neopixel → ImportError (no hay LED)
        }

    def _cargar(self, ejecutar):
        spec = importlib.util.spec_from_file_location(
            "felpudo_bajo_prueba_%d" % next(_contador), self.ruta)
        mod = importlib.util.module_from_spec(spec)
        mod.print = self._print
        self.mod = mod
        falsos = self._falsos()
        guardados = {k: sys.modules.get(k, _NADA) for k in falsos}
        sys.modules.update(falsos)
        # Sin esto importlib deja un __pycache__ al lado del firmware, que vive
        # en la carpeta de otra sesión.
        bytecode, sys.dont_write_bytecode = sys.dont_write_bytecode, True
        try:
            try:
                spec.loader.exec_module(mod)     # la v16 corre entera acá
            finally:
                sys.dont_write_bytecode = bytecode
                for k, v in guardados.items():
                    if v is _NADA:
                        sys.modules.pop(k, None)
                    else:
                        sys.modules[k] = v
            if ejecutar and not self.legacy:
                mod.main()
        except FinDeSimulacion:
            pass
        return mod

    def preparar(self):
        """Carga el firmware con los falsos pero sin arrancar main(), para
        probar funciones sueltas. Hay que asignar mod.hx / mod.kbd a mano."""
        if self.legacy:
            raise ValueError("la v16 corre al importarla: no se puede preparar")
        return self._cargar(ejecutar=False)

    def hx(self):
        """Un HX711 falso atado a esta simulación (para usar con preparar())."""
        return self._falsos()["adafruit_hx711.hx711"].HX711()

    def correr(self):
        self.reloj.fin_ns = self.t0_ns + int(self.escena.duracion_ms * 1e6)
        mod = self._cargar(ejecutar=True)
        return Resultado(self, mod)


# ─────────────────────────── evaluación ───────────────────────

class Resultado:
    def __init__(self, sim, mod):
        self.sim = sim
        self.mod = mod
        self.escena = sim.escena
        self.legacy = sim.legacy
        self.fin_ms = sim.escena.duracion_ms
        self.teclas = list(sim.teclas)
        self.salida = list(sim.salida)
        self.retenciones = list(sim.retenciones)
        self.trabadas = set(sim.pc_apretadas)
        self.errores = [(t, s) for t, s in self.salida if s.startswith("ERROR")]
        self.lecturas = sim.celda.lecturas
        self.reinicios_hx = list(sim.celda.reinicios)

    def evaluar(self, latencia_max_ms):
        return Evaluacion(self, latencia_max_ms)

    def traza(self, t_ms, antes_ms=3000, despues_ms=2000):
        """Lo que imprimió el firmware alrededor de t_ms, mezclado con la verdad."""
        a, b = t_ms - antes_ms, t_ms + despues_ms
        filas = [(t, "   " + s) for t, s in self.salida if a <= t <= b]
        filas += [(t, ">> TECLA %s" % NOMBRE_TECLA.get(k, k)) for t, k in self.teclas if a <= t <= b]
        for v in self.escena.visitas:
            if a <= v.t_sube <= b:
                filas.append((v.t_sube, "** sube una persona de %.0f kg" % v.kg))
            if a <= v.t_baja <= b:
                filas.append((v.t_baja, "** se baja la persona de %.0f kg" % v.kg))
        filas += [(t, "~~ el crudo salta %+.0f kg (artefacto)" % kg)
                  for t, kg in self.escena.saltos if a <= t <= b]
        filas += [(t, "~~ glitch %s %s x%d" % (tipo, valor, n))
                  for t, tipo, valor, n in self.escena.glitches if a <= t <= b]
        filas.sort(key=lambda f: f[0])
        cabeza = "   semilla %d, %s:" % (self.escena.semilla, os.path.basename(self.sim.ruta))
        return "\n".join([cabeza] + ["%10.3f s  %s" % (t / 1000, s) for t, s in filas])


class Evaluacion:
    """Compara las teclas que le llegaron a la PC con la verdad.

    v17: TECLA_SUBIO pone el juego en OCUPADO y TECLA_BAJO en VACIO (son
    idempotentes). v16: SPACE es un toggle.
    - falsas: teclas que cambiaron el estado del juego sin que nadie subiera
      o bajara.
    - tardias: teclas que corresponden a una subida/bajada real pero llegaron
      más de latencia_max_ms después.
    - perdidas: subidas/bajadas reales que el juego nunca se enteró.
    - duplicadas: teclas que no cambiaron nada (no molestan, se informan).
    - desfase_ms: tiempo total con el juego distinto de la verdad, fuera de
      la ventana de latencia después de cada transición real.
    """

    def __init__(self, res, latencia_max_ms):
        self.res = res
        self.latencia_max_ms = latencia_max_ms
        esc = res.escena
        transiciones = esc.transiciones()
        esperadas = [[t, e, False] for t, e in transiciones]
        if not res.legacy:
            subio, bajo = res.mod.TECLA_SUBIO, res.mod.TECLA_BAJO
        juego = VACIO
        self.cambios = [(0.0, VACIO)]
        self.falsas, self.tardias, self.duplicadas, self.ajenas = [], [], [], []
        self.latencias = []
        for t, k in res.teclas:
            if res.legacy:
                if k != Keycode.SPACE:
                    self.ajenas.append((t, k))
                    continue
                objetivo = OCUPADO if juego == VACIO else VACIO
            elif k == subio:
                objetivo = OCUPADO
            elif k == bajo:
                objetivo = VACIO
            else:
                self.ajenas.append((t, k))
                continue
            if objetivo == juego:
                self.duplicadas.append((t, k))
                continue
            juego = objetivo
            self.cambios.append((t, juego))
            for e in esperadas:
                if not e[2] and e[1] == objetivo and e[0] <= t <= e[0] + latencia_max_ms:
                    e[2] = True
                    self.latencias.append(t - e[0])
                    break
            else:
                # ¿Es la tecla de una transición real que llegó tarde? Solo si
                # la verdad ya está en ese estado y nadie más la reclamó.
                tarde = [e for e in esperadas if not e[2] and e[1] == objetivo and e[0] <= t]
                verdad = OCUPADO if esc.arriba(t) else VACIO
                if tarde and verdad == objetivo:
                    tarde[-1][2] = True
                    self.latencias.append(t - tarde[-1][0])
                    self.tardias.append((tarde[-1][0], t, objetivo))
                else:
                    self.falsas.append((t, objetivo))
        self.transiciones = transiciones
        self.esperadas = len(esperadas)
        self.perdidas = [(t, e) for t, e, ok in esperadas
                         if not ok and t + latencia_max_ms <= res.fin_ms]
        self.juego_final = juego
        self.verdad_final = OCUPADO if esc.arriba(res.fin_ms - 1) else VACIO
        self.desfase_ms, self.primer_desfase_ms = self._desfase(transiciones, res.fin_ms)

    def _desfase(self, transiciones, fin_ms, paso=10.0):
        verdad = [(0.0, VACIO)] + transiciones
        tol = [t for t, _ in transiciones]
        lat = self.latencia_max_ms
        iv = ij = it = 0
        total, primero, t = 0.0, None, 0.0
        while t < fin_ms:
            while iv + 1 < len(verdad) and verdad[iv + 1][0] <= t:
                iv += 1
            while ij + 1 < len(self.cambios) and self.cambios[ij + 1][0] <= t:
                ij += 1
            while it < len(tol) and tol[it] + lat < t:
                it += 1
            en_tolerancia = it < len(tol) and tol[it] <= t
            if verdad[iv][1] != self.cambios[ij][1] and not en_tolerancia:
                total += paso
                if primero is None:
                    primero = t
            t += paso
        return total, primero

    def transicion_anterior(self, t_ms):
        """La última transición real antes de t_ms (para arrancar la traza
        donde empezó el problema y no donde se notó)."""
        previas = [t for t, _ in self.transiciones if t < t_ms]
        return previas[-1] if previas else 0.0

    def percentil(self, p):
        if not self.latencias:
            return None
        s = sorted(self.latencias)
        return s[min(len(s) - 1, int(round(p / 100 * (len(s) - 1))))]

    def resumen(self):
        med = statistics.median(self.latencias) if self.latencias else None
        p95 = self.percentil(95)
        return ("transiciones=%d teclas=%d falsas=%d tardías=%d perdidas=%d duplicadas=%d "
                "desfase=%.1f s latencia mediana=%s p95=%s max=%s" % (
                    self.esperadas, len(self.res.teclas), len(self.falsas), len(self.tardias),
                    len(self.perdidas), len(self.duplicadas), self.desfase_ms / 1000,
                    "%.0f ms" % med if med is not None else "--",
                    "%.0f ms" % p95 if p95 is not None else "--",
                    "%.0f ms" % max(self.latencias) if self.latencias else "--"))
