namespace IngameScript
{
    partial class Program
    {
        private class CharacterAnimation
        {
            private char[] _frames;
            private int _cooldown;

            public int CurrentFrame { get; private set; }

            public char CurrentChar
            {
                get
                {
                    return _frames[CurrentFrame];
                }
            }

            public int FrameCount { get; }

            public int FrameTimeMilliseconds { get; set; }

            public CharacterAnimation(char[] frames, int frameTime)
            {
                _frames = frames;
                FrameCount = frames.Length;
                FrameTimeMilliseconds = frameTime;
            }

            public void Update(float delta)
            {
                var deltaMillis = (int)(delta * 1000);
                _cooldown += deltaMillis;

                if (_cooldown >= FrameTimeMilliseconds)
                {
                    CurrentFrame++;

                    if (CurrentFrame == FrameCount)
                    {
                        CurrentFrame = 0;
                    }

                    _cooldown -= FrameTimeMilliseconds;
                }
            }

            public override string ToString()
            {
                return $"{CurrentChar}";
            }
        }
    }
}
