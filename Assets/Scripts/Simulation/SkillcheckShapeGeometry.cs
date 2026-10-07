using BuenosDias.Config;
using UnityEngine;

namespace BuenosDias.Simulation
{
    /// <summary>
    /// Geometría pura de las formas del skillcheck, en píxeles de la textura del
    /// aro (128 × 128, centro en 0,0, Y hacia arriba). Está fuera de cualquier
    /// <c>MonoBehaviour</c> para poder testearla sin entrar en Play Mode.
    ///
    /// Convenio de ángulos, el mismo del aro: cero ARRIBA y creciendo en HORARIO.
    /// </summary>
    public static class SkillcheckShapeGeometry
    {
        private const float TwoPi = Mathf.PI * 2f;

        /// <summary>Radio del círculo, en el borde exterior del canal.</summary>
        public const float CircleRadius = 55f;

        /// <summary>Semilado del cuadrado.</summary>
        public const float SquareHalfSide = 43f;

        /// <summary>Radio circunscrito del triángulo (distancia centro-vértice).</summary>
        public const float TriangleCircumradius = 54f;

        /// <summary>Radio circunscrito de los polígonos regulares y de la estrella.</summary>
        public const float PolygonCircumradius = 55f;

        /// <summary>Radio de los vértices que se meten en la estrella.</summary>
        public const float StarInnerRadius = 24f;

        private const float TriangleInradius = TriangleCircumradius * 0.5f;

        // Normales salientes del triángulo: base, lado derecho y lado izquierdo.
        private const float Cos30 = 0.8660254f;

        /// <summary>Ancho del canal donde se pinta la zona, en píxeles.</summary>
        public static float ChannelWidth(SkillcheckShape shape)
        {
            switch (shape)
            {
                case SkillcheckShape.Triangulo:
                case SkillcheckShape.Rombo:
                case SkillcheckShape.Estrella:
                    return 7f;
                default:
                    return 9f;
            }
        }

        /// <summary>
        /// Qué tan adentro de la forma está el punto: positivo adentro, negativo
        /// afuera, cero sobre el borde. La pista es una banda de esta distancia.
        /// </summary>
        public static float InwardDistance(SkillcheckShape shape, float x, float y)
        {
            switch (shape)
            {
                case SkillcheckShape.Cuadrado:
                    return SquareHalfSide - Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));

                case SkillcheckShape.Triangulo:
                    float bottom = TriangleInradius + y;
                    float right = TriangleInradius - (Cos30 * x + 0.5f * y);
                    float left = TriangleInradius - (-Cos30 * x + 0.5f * y);
                    return Mathf.Min(bottom, Mathf.Min(right, left));

                case SkillcheckShape.Rombo:
                case SkillcheckShape.Pentagono:
                case SkillcheckShape.Hexagono:
                case SkillcheckShape.Octagono:
                case SkillcheckShape.Estrella:
                    return PolygonInward(PolygonFor(shape), x, y);

                default:
                    return CircleRadius - Mathf.Sqrt(x * x + y * y);
            }
        }

        /// <summary>
        /// Distancia del centro al borde siguiendo el rayo de ese ángulo. Es hasta
        /// dónde llega la aguja.
        /// </summary>
        public static float BoundaryRadius(SkillcheckShape shape, float angle)
        {
            float dx = Mathf.Sin(angle);
            float dy = Mathf.Cos(angle);

            switch (shape)
            {
                case SkillcheckShape.Cuadrado:
                    return SquareHalfSide / Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));

                case SkillcheckShape.Triangulo:
                    float best = float.MaxValue;
                    best = Mathf.Min(best, Reach(0f, -1f, dx, dy));
                    best = Mathf.Min(best, Reach(Cos30, 0.5f, dx, dy));
                    best = Mathf.Min(best, Reach(-Cos30, 0.5f, dx, dy));
                    return best;

                case SkillcheckShape.Rombo:
                case SkillcheckShape.Pentagono:
                case SkillcheckShape.Hexagono:
                case SkillcheckShape.Octagono:
                case SkillcheckShape.Estrella:
                    return PolygonReach(PolygonFor(shape), dx, dy);

                default:
                    return CircleRadius;
            }
        }

        /// <summary>Hasta dónde llega el rayo antes de cruzar un lado de normal (nx, ny).</summary>
        private static float Reach(float nx, float ny, float dx, float dy)
        {
            float facing = nx * dx + ny * dy;
            return facing <= 1e-5f ? float.MaxValue : TriangleInradius / facing;
        }

        // --- polígonos -------------------------------------------------------

        private static Vector2[] rombo, pentagono, hexagono, octagono, estrella;

        /// <summary>Vértices de la forma, en sentido horario empezando por arriba.</summary>
        private static Vector2[] PolygonFor(SkillcheckShape shape)
        {
            switch (shape)
            {
                case SkillcheckShape.Rombo: return rombo ?? (rombo = Regular(4));
                case SkillcheckShape.Pentagono: return pentagono ?? (pentagono = Regular(5));
                case SkillcheckShape.Hexagono: return hexagono ?? (hexagono = Regular(6));
                case SkillcheckShape.Octagono: return octagono ?? (octagono = Regular(8));
                default: return estrella ?? (estrella = Star(5));
            }
        }

        private static Vector2[] Regular(int sides)
        {
            var points = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = TwoPi * i / sides;
                points[i] = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * PolygonCircumradius;
            }
            return points;
        }

        private static Vector2[] Star(int tips)
        {
            var points = new Vector2[tips * 2];
            for (int i = 0; i < points.Length; i++)
            {
                float a = TwoPi * i / points.Length;
                float r = i % 2 == 0 ? PolygonCircumradius : StarInnerRadius;
                points[i] = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
            }
            return points;
        }

        /// <summary>
        /// Distancia con signo al borde del polígono: positiva adentro. Sirve igual
        /// para convexos y para la estrella, que no lo es.
        /// </summary>
        private static float PolygonInward(Vector2[] poly, float x, float y)
        {
            float best = float.MaxValue;
            bool inside = false;

            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];

                // par-impar: cruza el rayo horizontal hacia la derecha
                if ((a.y > y) != (b.y > y) &&
                    x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;

                Vector2 e = b - a;
                float t = Mathf.Clamp01(((x - a.x) * e.x + (y - a.y) * e.y) / e.sqrMagnitude);
                float dx = x - (a.x + e.x * t), dy = y - (a.y + e.y * t);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
            }

            return inside ? best : -best;
        }

        /// <summary>Hasta dónde llega el rayo desde el centro antes de cruzar el borde.</summary>
        private static float PolygonReach(Vector2[] poly, float dx, float dy)
        {
            float best = float.MaxValue;

            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j];
                Vector2 e = poly[i] - a;

                float denom = dx * e.y - dy * e.x;
                if (Mathf.Abs(denom) < 1e-6f) continue;

                float t = (a.x * e.y - a.y * e.x) / denom;
                float u = (a.x * dy - a.y * dx) / denom;
                if (t > 0f && u >= -1e-4f && u <= 1f + 1e-4f) best = Mathf.Min(best, t);
            }

            return best == float.MaxValue ? PolygonCircumradius : best;
        }
    }
}
