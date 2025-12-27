namespace RadHopper.Transport.Exceptions;

internal class ConnectorException : Exception
{
    public ConnectorException(string message) : base(message) { }
}