using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace Monocle
{
    public class ParticleEmitter : Component
    {

        public ParticleSystem System;
        public ParticleType Type;

        public Entity Track;
        public float Interval;
        public Vector3 Position;
        public Vector3 Range;
        public int Amount;
        public float? Direction;

        private float timer = 0f;

        public ParticleEmitter(ParticleSystem system, ParticleType type, Vector3 position, Vector3 range, int amount, float interval) : base(true, false)
        {
            System = system;
            Type = type;
            Position = position;
            Range = range;
            Amount = amount;
            Interval = interval;
        }

        public ParticleEmitter(ParticleSystem system, ParticleType type, Vector3 position, Vector3 range, float direction, int amount, float interval) 
            : this(system, type, position, range, amount, interval)
        {
            Direction = direction;
        }

        public ParticleEmitter(ParticleSystem system, ParticleType type, Entity track, Vector3 position, Vector3 range, float direction, int amount, float interval)
            : this(system, type, position, range, amount, interval)
        {
            Direction = direction;
            Track = track;
        }

        public void SimulateCycle()
        {
            Simulate(Type.LifeMax);
        }

        public void Simulate(float duration)
        {
            var steps = duration / Interval;
            for (var i = 0; i < steps; i++)
            {
                for (int j = 0; j < Amount; j++)
                {
                    // create the particle
                    var particle = new Particle();
                    var pos = Entity.Position + Position + Calc.Random.Range(-Range, Range);
                    if (Direction.HasValue)
                        particle = Type.Create(ref particle, pos, Direction.Value);
                    else
                        particle = Type.Create(ref particle, pos);
                    particle.Track = Track;

                    // simulate for a duration
                    var simulateFor = duration - Interval * i;
                    if (particle.SimulateFor(simulateFor))
                        System.Add(particle);
                }
            }
        }

        public void Emit()
        {
            if (Direction.HasValue)
                System.Emit(Type, Track, Amount, Entity.Position + Position, Range, Direction.Value);
            else
                System.Emit(Type, Track, Amount, Entity.Position + Position, Range, Type.Direction);
        }

        public void Emit(int _amount) {
            if (Direction.HasValue)
                System.Emit(Type, Track, _amount, Entity.Position + Position, Range, Direction.Value);
            else
                System.Emit(Type, Track, _amount, Entity.Position + Position, Range, Type.Direction);
        }

        public override void Update()
        {
            timer -= Engine.DeltaTime;
            if (timer <= 0)
            {
                timer = Interval;
                Emit();
            }
        }

    }
}
