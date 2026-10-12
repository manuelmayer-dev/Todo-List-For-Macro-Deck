namespace TodoList.Core.Storage;

/// <summary>
/// The local database could not be read or written. The message is diagnostic, not user-facing.
/// </summary>
public sealed class TodoStoreException(string message, Exception innerException) : Exception(message, innerException);
