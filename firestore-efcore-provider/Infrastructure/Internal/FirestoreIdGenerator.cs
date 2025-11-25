using System;
using System.Text;

namespace Firestore.EntityFrameworkCore.Infrastructure.Internal
{
    public class FirestoreIdGenerator : IFirestoreIdGenerator
    {
        private const string AutoIdAlphabet = 
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private const int AutoIdLength = 20;
        private readonly Random _random;

        public FirestoreIdGenerator()
        {
            _random = new Random();
        }

        public string GenerateId()
        {
            var builder = new StringBuilder(AutoIdLength);
            var maxRandom = AutoIdAlphabet.Length;

            for (int i = 0; i < AutoIdLength; i++)
            {
                var randomIndex = _random.Next(maxRandom);
                builder.Append(AutoIdAlphabet[randomIndex]);
            }

            return builder.ToString();
        }
    }
}
