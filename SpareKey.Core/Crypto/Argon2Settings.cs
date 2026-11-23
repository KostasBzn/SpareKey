namespace SpareKey.Core.Crypto
{
    internal class Argon2Settings
    {
        // Limits basically the requested recources
        // a damaged or malicious file could ask for 100 GB of memory
        // and freeze the PC before we even get to the password check
        public const int MinMemoryKiB = 8 * 1024; // thats binary, translates to 8KB in decimal
        public const int MaxMemoryKiB = 1024 * 1024; // same 1GB in decimal
        public const int MinIterations = 1;
        public const int MaxIterations = 10;
        public const int MinParallelism = 1;
        public const int MaxParallelism = 16;

        /// <summary>Memory in kidibyte.</summary>
        public int MemoryKiB { get; set; }

        /// <summary>How many passes over the memory. The more the slower.</summary>
        public int Iterations { get; set; }

        /// <summary>How many threads.</summary>
        public int Parallelism { get; set; }

        /// <summary>
        /// Settings for new vaults. 64 MiB, 3 passes, 4 lanes is one of the
        /// two recommended setups in RFC 9106 (the Argon2 standard).
        /// </summary>
        public static Argon2Settings CreateDefault()
        {
            Argon2Settings settings = new Argon2Settings();
            settings.MemoryKiB = 64 * 1024;
            settings.Iterations = 3;
            settings.Parallelism = 4;
            return settings;
        }

        /// <summary>
        /// Throws if any value is outside the allowed range.
        /// Call this before using settings that were read from a file.
        /// </summary>
        public void Validate()
        {
            if (MemoryKiB < MinMemoryKiB || MemoryKiB > MaxMemoryKiB)
            {
                throw new ArgumentOutOfRangeException(nameof(MemoryKiB), MemoryKiB, "Argon2 memory is outside the allowed range.");
            }
            if (Iterations < MinIterations || Iterations > MaxIterations)
            {
                throw new ArgumentOutOfRangeException(nameof(Iterations), Iterations, "Argon2 iterations are outside the allowed range.");
            }
            if (Parallelism < MinParallelism || Parallelism > MaxParallelism)
            {
                throw new ArgumentOutOfRangeException(nameof(Parallelism), Parallelism, "Argon2 parallelism is outside the allowed range.");
            }
        }
    }
}