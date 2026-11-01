using LibraryCode;
using Moonwell;

namespace LibraryMapFixture
{
    /// <summary>A map whose package code is all in a library project: its own code names only the library's component, so its assembly references none of the packages.</summary>
    public sealed class LibraryMap : Map
    {
        public LibraryMap()
        {
            Name = "LibraryMap";
            Components.Add(new Opening());
        }
    }
}
