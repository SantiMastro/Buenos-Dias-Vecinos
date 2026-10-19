"""Pruebas de aceptación del firmware del felpudo (la balanza).

Corren el firmware real (por defecto la v17 de la sesión del felpudo) contra el
arnés de arnes.py: sin placa, sin puerto serie, sin tocar F:\\.

Correr todo (desde cualquier carpeta):
    py -I -m unittest discover -s "<esta carpeta>" -v

Variables opcionales:
    FELPUDO_CODE      firmware a probar (por defecto la v17 de la sesión del felpudo)
    FELPUDO_V16       la v16, solo para la prueba de control del arnés
    FELPUDO_SEMILLAS  semillas por escenario (8)
    FELPUDO_MINUTOS   minutos de los escenarios largos (5; la corrida completa usa 10)

Una falla imprime la traza del firmware alrededor del problema, mezclada con
lo que pasaba de verdad arriba del felpudo (** personas, ~~ artefactos del sensor).
"""

import os
import random
import statistics
import sys
import unittest

import arnes
from arnes import Escena, Keycode, OCUPADO, Simulacion, VACIO

# ── Lo que se le exige al felpudo. Si cambia el diseño, se cambia acá. ──
LATENCIA_MAX_MS = 1000     # una pisada tiene que llegarle al juego en menos de 1 s
RETENCION_MAX_MS = 250     # más que esto y Windows empieza a repetir la tecla
ARRANQUE_S = 10            # el firmware necesita ~4 s de historial; nadie pisa antes

SEMILLAS = int(os.environ.get("FELPUDO_SEMILLAS", "8"))
MINUTOS = float(os.environ.get("FELPUDO_MINUTOS", "5"))
LARGO_S = MINUTOS * 60

_resumenes = []

try:                               # las trazas traen "→" y "⚠"
    sys.stderr.reconfigure(errors="backslashreplace")
except AttributeError:
    pass


def setUpModule():
    ruta = arnes.ruta_v17()
    if not os.path.isfile(ruta):
        raise FileNotFoundError(
            "No encuentro el firmware a probar: %s\n"
            "Pasale la ruta con la variable FELPUDO_CODE." % ruta)


def tearDownModule():
    if not _resumenes:
        return
    print("\n── Resumen (firmware: %s, %d semillas, %.0f min) ──"
          % (os.path.basename(arnes.ruta_v17()), SEMILLAS, MINUTOS), file=sys.stderr)
    for nombre, texto in _resumenes:
        print("  %-38s %s" % (nombre, texto), file=sys.stderr)


def semillas(base):
    return range(base, base + SEMILLAS)


def latencias_texto(latencias):
    if not latencias:
        return "sin transiciones"
    s = sorted(latencias)
    return "latencia mediana %.0f ms, p95 %.0f ms, máx %.0f ms (%d transiciones)" % (
        statistics.median(s), s[min(len(s) - 1, round(0.95 * (len(s) - 1)))], s[-1], len(s))


class Base(unittest.TestCase):

    def correr(self, escena, ruta=None, permitir_errores=False, retencion_max_ms=RETENCION_MAX_MS,
               **kw):
        res = Simulacion(escena, ruta or arnes.ruta_v17(), **kw).correr()
        if res.errores and not permitir_errores:
            t, _ = res.errores[0]
            self.fail("el loop atrapó %d excepción(es):\n%s" % (len(res.errores), res.traza(t)))
        if res.trabadas:
            self.fail("quedó apretada en la PC: %s\n%s" % (
                [arnes.NOMBRE_TECLA.get(k, k) for k in res.trabadas], res.traza(res.fin_ms)))
        largas = [(k, ms) for k, ms in res.retenciones if ms > retencion_max_ms]
        if largas:
            self.fail("la PC vio teclas apretadas más de %d ms (autorepetición): %s"
                      % (retencion_max_ms, [(arnes.NOMBRE_TECLA.get(k, k), round(ms)) for k, ms in largas]))
        return res

    def evaluar(self, res):
        return res.evaluar(LATENCIA_MAX_MS)

    def exigir_sin_teclas(self, res):
        if res.teclas:
            t, k = res.teclas[0]
            self.fail("mandó %d tecla(s) sin que nadie pisara; la primera, %s a los %.1f s:\n%s"
                      % (len(res.teclas), arnes.NOMBRE_TECLA.get(k, k), t / 1000, res.traza(t)))

    def exigir_sin_falsas(self, res, ev):
        if ev.falsas:
            t, e = ev.falsas[0]
            self.fail("%d tecla(s) falsa(s); la primera, %s a los %.1f s:\n%s" % (
                len(ev.falsas), "SUBIO" if e == OCUPADO else "BAJO", t / 1000, res.traza(t)))

    def exigir_a_tiempo(self, res, ev):
        if ev.tardias:
            t_real, t, e = ev.tardias[0]
            self.fail("%d tecla(s) tarde; la primera, %s %.0f ms después de que %s (máx %d ms):\n%s"
                      % (len(ev.tardias), "SUBIO" if e == OCUPADO else "BAJO", t - t_real,
                         "subió alguien" if e == OCUPADO else "se bajó el último",
                         LATENCIA_MAX_MS, res.traza(t, despues_ms=1000)))

    def _traza_desde_la_causa(self, res, ev, t):
        desde = ev.transicion_anterior(t) - 1000
        return res.traza(t, antes_ms=t - desde, despues_ms=2500)

    def exigir_sin_perdidas(self, res, ev):
        if ev.perdidas:
            t, e = ev.perdidas[0]
            self.fail("%d transición(es) que el juego no se enteró; la primera, %s a los %.1f s:\n%s"
                      % (len(ev.perdidas), "subida" if e == OCUPADO else "bajada", t / 1000,
                         self._traza_desde_la_causa(res, ev, t)))

    def exigir_en_sincro(self, res, ev):
        if ev.desfase_ms > 0:
            t = ev.primer_desfase_ms
            self.fail("el juego quedó %.1f s desincronizado de la verdad; desde los %.1f s:\n%s"
                      % (ev.desfase_ms / 1000, t / 1000, self._traza_desde_la_causa(res, ev, t)))
        self.assertEqual(ev.juego_final, ev.verdad_final, "terminó con el juego en otro estado")

    def exigir_todo(self, res, ev):
        self.exigir_sin_falsas(res, ev)
        self.exigir_a_tiempo(res, ev)
        self.exigir_sin_perdidas(res, ev)
        self.exigir_en_sincro(res, ev)

    def teclas(self, res, desde_s=0, hasta_s=1e9):
        return [(t / 1000, arnes.NOMBRE_TECLA.get(k, k)) for t, k in res.teclas
                if desde_s * 1000 <= t < hasta_s * 1000]


# ─────────────────────────── lectura del HX711 ───────────────────────────

class A_Lectura(Base):

    def test_el_arnes_reproduce_el_bug_de_signo_de_la_lib(self):
        self.assertEqual(arnes.como_devuelve_la_lib(-1), 0xFFFFFFFF)
        self.assertEqual(arnes.como_devuelve_la_lib(-100_000), 2 ** 32 - 100_000)
        self.assertEqual(arnes.como_devuelve_la_lib(700_000), 700_000)

    def _leer_una(self, crudo):
        esc = Escena(duracion_s=5, crudo_inicial=crudo, deriva_kg_s=None, ruido_kg=0)
        sim = Simulacion(esc, arnes.ruta_v17())
        mod = sim.preparar()
        mod.hx = sim.hx()
        sim.reloj.avanzar_a_ns(sim.t0_ns + 500_000_000)
        return sim, mod, mod.leer()

    def test_leer_corrige_el_signo(self):
        for crudo in (-100_000, -1, 0, 700_000, -8_000_000):
            with self.subTest(crudo=crudo):
                self.assertEqual(self._leer_una(crudo)[2], crudo)

    def test_leer_no_bloquea_si_no_hay_dato_nuevo(self):
        sim, mod, primera = self._leer_una(700_000)
        self.assertIsNotNone(primera)
        antes = sim.reloj.ns
        self.assertIsNone(mod.leer(), "leyó dos veces la misma conversión")
        self.assertEqual(sim.reloj.ns, antes, "leer() se quedó esperando al HX711")

    def test_toma_una_muestra_por_conversion(self):
        res = self.correr(Escena(semilla=3, duracion_s=30))
        por_segundo = res.lecturas / (30 - 2)          # 2 s de arranque
        self.assertGreaterEqual(por_segundo, 0.95 * 10.6,
                                "lee %.1f muestras/s con el HX711 a 10,6 SPS" % por_segundo)


# ─────────────────────────── felpudo vacío ───────────────────────────

class B_FelpudoVacio(Base):
    """Nadie pisa: cualquier tecla es un falso positivo."""

    def test_no_manda_tecla_al_arrancar(self):
        for s in semillas(100):
            with self.subTest(semilla=s):
                self.exigir_sin_teclas(self.correr(Escena(semilla=s, duracion_s=15)))

    def test_reposo_con_deriva(self):
        for s in semillas(200):
            with self.subTest(semilla=s):
                self.exigir_sin_teclas(self.correr(Escena(semilla=s, duracion_s=LARGO_S)))

    def test_reposo_con_ruido_alto(self):
        for s in semillas(300):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=LARGO_S, ruido_kg=2.0)
                self.exigir_sin_teclas(self.correr(esc))

    def test_salto_real_del_log(self):
        """El falso SUBIO que se vio en el felpudo: crudo 619 584 → 876 769 de
        golpe con el felpudo vacío, mientras derivaba para abajo."""
        for s in semillas(400):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=60, crudo_inicial=619_584,
                             deriva_kg_s=(-2.1, -0.9))
                esc.agregar_salto(30, (876_769 - 619_584) / arnes.UNIDADES_POR_KG)
                self.exigir_sin_teclas(self.correr(esc))

    def test_dientes_de_sierra(self):
        for s in semillas(500):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=LARGO_S)
                esc.agregar_sierra(ARRANQUE_S, LARGO_S - 2, kg=(20, 30, 40, 60))
                self.exigir_sin_teclas(self.correr(esc))

    def _glitches(self, s, cuantas):
        esc = Escena(semilla=s, duracion_s=LARGO_S)
        r = random.Random(s)
        t = ARRANQUE_S
        while t < LARGO_S - 3:
            tipo = r.choice(["pico", "pico", "bit", "fijo"])
            if tipo == "pico":
                valor = r.choice([-1, 1]) * r.uniform(20, 460)
            elif tipo == "bit":
                valor = r.choice([18, 19, 20, 21])
            else:
                valor = r.choice([0x7FFFFF, -0x800000])
            esc.agregar_glitch(t, tipo, valor, cuantas)
            t += r.uniform(5, 15)
        return esc

    def test_glitch_aislado(self):
        """Una muestra rota (pico de 20–460 kg, bit 18–21 invertido o saturada)."""
        for s in semillas(600):
            with self.subTest(semilla=s):
                self.exigir_sin_teclas(self.correr(self._glitches(s, cuantas=1)))

    def test_dos_glitches_seguidos(self):
        for s in semillas(700):
            with self.subTest(semilla=s):
                self.exigir_sin_teclas(self.correr(self._glitches(s, cuantas=2)))

    def _corrimientos(self, s, ruido_kg):
        esc = Escena(semilla=s, duracion_s=LARGO_S, ruido_kg=ruido_kg)
        r = random.Random(s)
        t = ARRANQUE_S
        while t < LARGO_S - 6:
            kg = r.choice([-1, 1]) * r.uniform(5, 12)
            esc.agregar_corrimiento(t, kg, dur_s=r.choice([0, r.uniform(1, 5)]))
            t += r.uniform(10, 25)
        return esc

    def test_corrimientos_bajo_el_umbral(self):
        """Cambios de 5–12 kg, de golpe o en 1–5 s, para los dos lados."""
        for s in semillas(800):
            with self.subTest(semilla=s):
                self.exigir_sin_teclas(self.correr(self._corrimientos(s, ruido_kg=0.6)))

    def test_corrimientos_bajo_el_umbral_con_ruido_alto(self):
        """Lo mismo con 2 kg de ruido: un corrimiento chico no puede dejar una
        carga fantasma (S falsa y OCUPADO trabado)."""
        for s in semillas(850):
            with self.subTest(semilla=s):
                self.exigir_sin_teclas(self.correr(self._corrimientos(s, ruido_kg=2.0)))


# ─────────────────────────── con gente ───────────────────────────

class C_Personas(Base):

    def test_una_persona_por_vez(self):
        """Visitas de 2–20 s, 50–120 kg: una tecla al subir y una al bajar."""
        latencias = []
        for s in semillas(900):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=LARGO_S)
                esc.agregar_visitas(ARRANQUE_S, LARGO_S - 3)
                res = self.correr(esc)
                ev = self.evaluar(res)
                latencias += ev.latencias
                self.exigir_todo(res, ev)
                self.assertEqual(ev.duplicadas, [], "mandó teclas repetidas")
        _resumenes.append(("una persona por vez (10,6 SPS)", latencias_texto(latencias)))

    def test_dos_personas_que_se_pisan(self):
        """A sube, B sube, A baja, B baja: SUBIO una vez y BAJO una vez, al final."""
        for s in semillas(1000):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=120)
                r = random.Random(s)
                for t in (ARRANQUE_S, 50, 90):
                    a = esc.agregar_visita(t, quieto_s=r.uniform(8, 15))
                    b_sube = t + r.uniform(0.3, 5)
                    b_baja = a.t_baja / 1000 + r.uniform(0.3, 5)
                    esc.agregar_visita(b_sube, quieto_s=b_baja - b_sube - 0.3, rampa_ms=300)
                res = self.correr(esc)
                ev = self.evaluar(res)
                self.exigir_todo(res, ev)
                self.assertEqual(len(res.teclas), 6, "esperaba S,B tres veces: %s" % self.teclas(res))

    def test_dos_personas_suben_casi_juntas(self):
        """La segunda sube 0,4–0,8 s después, mientras se mide a la primera: no
        se puede contar dos veces (quedaría carga fantasma y OCUPADO trabado)."""
        for separacion in (0.4, 0.6, 0.8):
            for s in semillas(1050):
                with self.subTest(separacion_s=separacion, semilla=s):
                    esc = Escena(semilla=s, duracion_s=45)
                    esc.agregar_visita(ARRANQUE_S, quieto_s=14)
                    esc.agregar_visita(ARRANQUE_S + separacion, quieto_s=18)
                    res = self.correr(esc)
                    self.exigir_todo(res, self.evaluar(res))

    def test_personas_pesadas(self):
        """100–120 kg sin que el crudo cruce el cero: la bajada tiene que llegar a tiempo."""
        for s in semillas(1150):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=LARGO_S, crudo_inicial=2_500_000)
                esc.agregar_visitas(ARRANQUE_S, LARGO_S - 3, kg=(100, 120))
                res = self.correr(esc)
                self.exigir_todo(res, self.evaluar(res))

    def test_toques_rapidos(self):
        """Pisar y salir en 0,3–1,5 s: no puede quedar trabado en OCUPADO."""
        for quieto in (0.3, 0.6, 1.0, 1.5):
            for s in semillas(1100):
                with self.subTest(quieto_s=quieto, semilla=s):
                    esc = Escena(semilla=s, duracion_s=70)
                    for t in range(ARRANQUE_S, 64, 5):
                        esc.agregar_visita(t, quieto_s=quieto)
                    res = self.correr(esc)
                    ev = self.evaluar(res)
                    self.exigir_sin_falsas(res, ev)      # perderse un toque está permitido
                    self.exigir_a_tiempo(res, ev)
                    self.exigir_en_sincro(res, ev)

    def test_persona_pesada_con_crudo_negativo(self):
        """150 kg con el crudo en +620 000: la lectura cruza el cero (bug de signo)."""
        for s in semillas(1200):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=60, crudo_inicial=620_000,
                             deriva_kg_s=(-0.5, 0.3))
                esc.agregar_visita(ARRANQUE_S, quieto_s=10, kg=150)
                esc.agregar_visita(35, quieto_s=10, kg=150)
                res = self.correr(esc)
                self.exigir_todo(res, self.evaluar(res))

    def test_salto_del_crudo_con_persona_de_90kg_arriba(self):
        """Un diente de sierra (+40 kg de crudo) con alguien parado no es una bajada."""
        for s in semillas(1300):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=45)
                esc.agregar_visita(ARRANQUE_S, quieto_s=20, kg=90)
                esc.agregar_salto(ARRANQUE_S + random.Random(s).uniform(4, 16), 40)
                res = self.correr(esc)
                self.exigir_todo(res, self.evaluar(res))


# ─────────────────────────── robustez ───────────────────────────

class D_Robustez(Base):

    def test_pausa_con_bloq_num(self):
        """Bloq Num = pausa (sin teclas). Al volver: BAJO para resincronizar."""
        for s in semillas(1400):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=80)
                esc.agregar_visita(ARRANQUE_S, quieto_s=4)
                esc.agregar_visita(25, quieto_s=8)        # en pausa
                esc.agregar_visita(38, quieto_s=20)       # en pausa, sigue arriba al volver
                esc.agregar_visita(65, quieto_s=4)
                res = self.correr(esc, numlock_toques_s=(20, 50))
                nombres = [k for _, k in self.teclas(res)]
                self.assertEqual(self.teclas(res, 20, 50), [], "mandó teclas en pausa")
                despues = self.teclas(res, 50)
                self.assertTrue(despues and despues[0][1] == "B" and despues[0][0] < 51,
                                "al volver tenía que mandar BAJO enseguida: %s" % despues)
                self.assertEqual(nombres, ["S", "B", "B", "S", "B"],
                                 "secuencia: %s\n%s" % (self.teclas(res), res.traza(50_000)))

    def test_bloq_num_prendido_al_conectar_no_pausa(self):
        for s in semillas(1500):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=60)
                esc.agregar_visitas(ARRANQUE_S, 57)
                res = self.correr(esc, numlock_inicial=True)
                self.exigir_todo(res, self.evaluar(res))

    def test_usb_caido_justo_al_pisar(self):
        """El USB falla 2 s justo cuando sube alguien: la tecla sale cuando vuelve."""
        for s in semillas(1600):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=40)
                esc.agregar_visita(ARRANQUE_S, quieto_s=8)
                res = self.correr(esc, usb_cortes_s=[(ARRANQUE_S - 0.1, ARRANQUE_S + 1.9)])
                t = self.teclas(res)
                self.assertEqual([k for _, k in t], ["S", "B"], "teclas: %s" % t)
                self.assertLess(t[0][0], ARRANQUE_S + 1.9 + 0.2,
                                "tardó en reintentar cuando volvió el USB: %s" % t)

    def test_usb_se_cae_con_la_tecla_apretada(self):
        """Se cae justo entre press y release: cuando vuelve, la suelta."""
        for s in semillas(1700):
            with self.subTest(semilla=s):
                def escena():
                    e = Escena(semilla=s, duracion_s=40)
                    e.agregar_visita(ARRANQUE_S, quieto_s=8)
                    return e
                t_s = self.correr(escena()).teclas[0][0] / 1000
                res = self.correr(escena(), usb_cortes_s=[(t_s + 0.02, t_s + 2)],
                                  retencion_max_ms=10_000)
                self.assertEqual([k for _, k in self.teclas(res)], ["S", "B"])
                self.exigir_todo(res, self.evaluar(res))

    def test_usb_se_cae_con_la_tecla_apretada_y_se_baja(self):
        """Igual, pero la persona se baja mientras el USB sigue caído: cuando
        vuelve, la S se suelta y sale el BAJO (no puede quedar OCUPADO)."""
        for s in semillas(1750):
            with self.subTest(semilla=s):
                def escena():
                    e = Escena(semilla=s, duracion_s=30)
                    e.agregar_visita(ARRANQUE_S, quieto_s=1.0)
                    return e
                t_s = self.correr(escena()).teclas[0][0] / 1000
                vuelve = t_s + 3
                res = self.correr(escena(), usb_cortes_s=[(t_s + 0.02, vuelve)],
                                  retencion_max_ms=10_000)
                retenida = max((ms for _, ms in res.retenciones), default=0)
                self.assertLess(retenida, (vuelve - t_s) * 1000 + 200,
                                "la S quedó apretada %.1f s en la PC" % (retenida / 1000))
                b = [t for t, k in self.teclas(res) if k == "B"]
                self.assertTrue(b and b[0] < vuelve + 0.3,
                                "el BAJO no salió al volver el USB: %s\n%s"
                                % (self.teclas(res), res.traza(vuelve * 1000)))

    def test_hx711_desconectado_con_felpudo_vacio(self):
        for s in semillas(1800):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=70)
                esc.agregar_visita(40, quieto_s=8)
                res = self.correr(esc, hx_cortes_s=[(20, 23.5)])
                self.assertTrue(res.reinicios_hx, "no reinició el HX711 tras 3,5 s sin datos")
                self.exigir_todo(res, self.evaluar(res))

    def test_hx711_desconectado_con_persona_arriba(self):
        for s in semillas(1900):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=50)
                esc.agregar_visita(ARRANQUE_S, quieto_s=20)
                res = self.correr(esc, hx_cortes_s=[(15, 18.5)])
                self.exigir_todo(res, self.evaluar(res))

    def test_una_excepcion_del_hx711_no_frena_el_loop(self):
        for s in semillas(2000):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=60)
                esc.agregar_visita(30, quieto_s=8)
                res = self.correr(esc, hx_fallas_s=[20], permitir_errores=True)
                self.assertEqual(len(res.errores), 1)
                self.exigir_todo(res, self.evaluar(res))

    def test_una_semana_prendido(self):
        """Con el reloj en 7 días (time.monotonic() ya no tiene resolución)."""
        for s in semillas(2100):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=120)
                esc.agregar_visitas(ARRANQUE_S, 117)
                res = self.correr(esc, t0_ms=7 * 24 * 3600 * 1000)
                self.exigir_todo(res, self.evaluar(res))


# ─────────────────────────── jumper RATE soldado ───────────────────────────

class E_80SPS(Base):
    """Las mismas exigencias con el HX711 a 80 SPS."""

    def test_reposo(self):
        for s in semillas(2200):
            with self.subTest(semilla=s):
                self.exigir_sin_teclas(self.correr(Escena(semilla=s, duracion_s=LARGO_S), sps=80))

    def test_dientes_de_sierra(self):
        for s in semillas(2300):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=LARGO_S)
                esc.agregar_sierra(ARRANQUE_S, LARGO_S - 2, kg=(20, 30, 40, 60))
                self.exigir_sin_teclas(self.correr(esc, sps=80))

    def test_una_persona_por_vez(self):
        latencias = []
        for s in semillas(2400):
            with self.subTest(semilla=s):
                esc = Escena(semilla=s, duracion_s=LARGO_S)
                esc.agregar_visitas(ARRANQUE_S, LARGO_S - 3)
                res = self.correr(esc, sps=80)
                ev = self.evaluar(res)
                latencias += ev.latencias
                self.exigir_todo(res, ev)
        _resumenes.append(("una persona por vez (80 SPS)", latencias_texto(latencias)))


# ─────────────────────────── control del arnés ───────────────────────────

class F_ControlDelArnes(Base):
    """Si el arnés no logra que la v16 falle con el salto que se vio en el
    felpudo, el modelo es demasiado amable y las demás pruebas no valen."""

    def test_la_v16_cae_en_el_falso_positivo_del_log(self):
        ruta = arnes.ruta_v16()
        if not os.path.isfile(ruta):
            self.skipTest("no encuentro la v16 (FELPUDO_V16): %s" % ruta)
        con_falso = 0
        for s in semillas(400):
            esc = Escena(semilla=s, duracion_s=60, crudo_inicial=619_584,
                         deriva_kg_s=(-2.1, -0.9))
            esc.agregar_salto(30, (876_769 - 619_584) / arnes.UNIDADES_POR_KG)
            res = self.correr(esc, ruta=ruta)
            con_falso += any(k == Keycode.SPACE for _, k in res.teclas)
        self.assertEqual(con_falso, SEMILLAS,
                         "la v16 mandó SPACE en %d de %d semillas" % (con_falso, SEMILLAS))


if __name__ == "__main__":
    unittest.main()
