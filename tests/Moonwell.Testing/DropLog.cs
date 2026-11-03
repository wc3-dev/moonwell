namespace Moonwell.Testing
{
    /// <summary>An item that logs its label when it is dropped, with whether it had a carrier then.</summary>
    public class DropLog : Item
    {
        /// <summary>The label it logs.</summary>
        public string Label;

        /// <summary>Makes an item that logs a label.</summary>
        public DropLog(string label)
        {
            Label = label;
        }

        /// <summary>Logs "dropped", the label, and "carried" or "no carrier".</summary>
        public override void OnDropped(Unit by)
        {
            TestLog.Lines.Add("dropped " + Label + (Carrier == null ? " no carrier" : " carried"));
        }
    }
}
