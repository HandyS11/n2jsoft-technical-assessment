namespace Application.Exceptions;

public sealed class NotFoundException(string resource, object key, Exception? innerException = null)
    : Exception($"{resource} '{key}' was not found.", innerException);
