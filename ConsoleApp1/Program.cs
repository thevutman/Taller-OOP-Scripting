using System;
using System.Collections.Generic;
using System.Threading;
namespace ConsoleApp1
{
    abstract class Node
    {
        protected readonly List<Node> _children = new();
        public abstract bool Execute();
        public virtual void AddChild(Node c) { _children.Add(c); }
    }

    class Root : Node
    {
        public Root(Node child) { AddChild(child); }
        public override void AddChild(Node c)
        {
            if (_children.Count == 1) throw new InvalidOperationException("Root solo 1 hijo.");
            base.AddChild(c);
        }
        public override bool Execute() => _children[0].Execute();
    }

    abstract class Composite : Node
    {
        protected Composite(params Node[] children)
        {
            if (children.Length < 1) throw new ArgumentException("Composite ≥1 hijo");
            foreach (var c in children) AddChild(c);
        }
    }

    // Secuencia estándar: si un hijo falla, corta
    class Sequence : Composite
    {
        public Sequence(params Node[] children) : base(children) { }
        public override bool Execute()
        {
            foreach (var c in _children) if (!c.Execute()) return false;
            return true;
        }
    }

    // Selector con Check opcional; si Check=false, falla y NO ejecuta hijos
    class Selector : Composite
    {
        private readonly Func<bool> _check;
        public Selector(Func<bool>? check, params Node[] children) : base(children)
        {
            _check = check ?? (() => true);
        }
        public override bool Execute()
        {
            if (!_check()) { Console.WriteLine("[Selector] Check=false → FAIL"); return false; }
            foreach (var c in _children) if (c.Execute()) return true;
            return false;
        }
    }

    abstract class Task : Node
    {
        public override void AddChild(Node c) => throw new InvalidOperationException("Task no admite hijos");
    }

    class WaitTask : Task
    {
        private readonly int _ms;
        public WaitTask(int ms) { _ms = ms; }
        public override bool Execute() { Console.WriteLine($"→ Esperar {_ms} ms"); Thread.Sleep(_ms); return true; }
    }

    // ===== Mundo y tareas específicas =====
    struct Vec3
    {
        public double X, Y, Z; public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static double Dist(Vec3 a, Vec3 b) { var dx = a.X - b.X; var dy = a.Y - b.Y; var dz = a.Z - b.Z; return Math.Sqrt(dx * dx + dy * dy + dz * dz); }
        public static Vec3 StepTowards(Vec3 from, Vec3 to, double step)
        {
            double d = Dist(from, to); if (d <= step) return to;
            double t = step / d; return new Vec3(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t, from.Z + (to.Z - from.Z) * t);
        }
        public override string ToString() => $"({X:0.00},{Y:0.00},{Z:0.00})";
    }

    class World
    {
        public Vec3 Agent = new(0, 0, 0);
        public Vec3 Target = new(0.8, 0, 0);
        public bool IsAtDistance(double thr)
        {
            var d = Vec3.Dist(Agent, Target);
            Console.WriteLine($"→ Distancia={d:0.00} (thr={thr:0.00})");
            return d <= thr;
        }
    }

    // Mueve HASTA LLEGAR en un solo Execute(), imprimiendo progreso
    class MoveToTask : Task
    {
        private readonly World _w; private readonly double _step;
        public MoveToTask(World w, double step = 0.5) { _w = w; _step = step; }
        public override bool Execute()
        {
            Console.WriteLine("→ Comienza MoveTo");
            int it = 0;
            while (Vec3.Dist(_w.Agent, _w.Target) > 1e-3 && it < 200)
            {
                var before = _w.Agent;
                _w.Agent = Vec3.StepTowards(_w.Agent, _w.Target, _step);
                Console.WriteLine($"   · {_w.Agent}  (+{Vec3.Dist(before, _w.Agent):0.00})");
                Thread.Sleep(100); // “impresión periódica”
                it++;
            }
            Console.WriteLine("→ Llegó al objetivo.");
            return true;
        }
    }

    class Program
    {
        static void Main()
        {
            const double distanciaValida = 1.0;   // (1)
            const int tiempoEsperaMs = 300;       // (3)
            var world = new World();

            // Árbol exacto pedido:
            // Root → Sequence( Selector(sin check → hijo: Selector(check distancia → hijo: MoveTo)), Wait )
            var bt = new Root(
                new Sequence(
                    new Selector(null,                               // selector SIN evaluación
                        new Selector(() => world.IsAtDistance(distanciaValida),  // (1)
                            new MoveToTask(world, step: 0.5)          // (2)
                        )
                    ),
                    new WaitTask(tiempoEsperaMs)                      // (3)
                )
            );

            // Ejecuta varios ciclos
            for (int i = 1; i <= 3; i++)
            {
                Console.WriteLine($"\n=== CICLO {i} | Agent={world.Agent} Target={world.Target} ===");
                bt.Execute();
            }
            Console.WriteLine("\nListo.");
        }
    }
}