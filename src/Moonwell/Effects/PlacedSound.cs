using System;

namespace Moonwell
{
    /// <summary>
    /// A sound of the map's Sound Editor, played with the settings it has there (volume, pitch, 3D, channel, fades). The game
    /// has one of each: playing it while it plays starts it again. Got by its name with <see cref="Named"/>, or from
    /// <c>Placed.Sounds</c>.
    /// </summary>
    public sealed class PlacedSound
    {
        private readonly sound handle;

        private PlacedSound(string name, sound handle)
        {
            Name = name;
            this.handle = handle;
        }

        /// <summary>The name the Sound Editor gives it, without <c>gg_snd_</c>.</summary>
        public string Name { get; }

        /// <summary>The sound by its Sound Editor name without <c>gg_snd_</c>; null when the map has none of that name. The build checks a name written as a literal.</summary>
        public static PlacedSound? Named(string name)
        {
            var handle = Runtime.World.PlacedSound(name);
            return handle == null ? null : new PlacedSound(name, handle);
        }

        /// <summary>Plays it; a 3D sound from where it was last placed.</summary>
        public void Play()
        {
            Runtime.World.StartPlaying(handle);
        }

        /// <summary>Plays a 3D sound at a place.</summary>
        public void PlayAt(float x, float y)
        {
            Runtime.World.PlaceSound(handle, x, y, 0.0f);
            Runtime.World.StartPlaying(handle);
        }

        /// <summary>Plays a 3D sound on a unit, following it.</summary>
        public void PlayOn(Unit unit)
        {
            if (unit is null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            Runtime.World.PutSoundOn(handle, unit.Handle);
            Runtime.World.StartPlaying(handle);
        }

        /// <summary>Stops it, with the fade the Sound Editor gives it.</summary>
        public void Stop()
        {
            Runtime.World.StopPlacedSound(handle);
        }
    }
}
