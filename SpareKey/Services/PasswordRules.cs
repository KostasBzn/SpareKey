namespace SpareKey.Services
{
    /// <summary>
    /// The rules for a master password, in one place.
    /// </summary>
    public static class PasswordRules
    {
        public const int MinLength = 10;

        /// <summary>Returns an error message, or "" if the password is OK.</summary>
        public static string Check(string password, string confirmPassword)
        {
            if (password.Length < MinLength)
                return "The password needs at least " + MinLength + " characters.";

            if (password != confirmPassword)
                return "The two passwords don't match.";

            return "";
        }
    }
}
