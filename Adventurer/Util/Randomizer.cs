using System;

namespace Adventurer.Util
{
    public static class Randomizer
    {
        private static Random _random = new Random();

        public static int GetRandomNumber(int max)
        {
            return _random.Next(max);
        }
        public static int GetRandomNumber(int min, int max)
        {
            return _random.Next(min, max);
        }
    }
}
