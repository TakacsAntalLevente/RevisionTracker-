namespace RevisionTracker.Core
{
    /// <summary>Feature limits per edition. Licensing is not implemented yet; the free limits are enforced here.</summary>
    public static class Edition
    {
        public const string ProductName = "RevisionTracker";
        public const string Name = "Free";
        public const bool IsPro = false;

        /// <summary>Free edition keeps at most this many versions per document (0 = unlimited).</summary>
        public const int MaxVersionsPerDocument = 25;
    }
}
