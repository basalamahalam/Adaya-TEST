namespace OrderManagement.Api.Exceptions;

public abstract class ApiException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
    public abstract string ErrorCode { get; }
}

public class NotFoundApiException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public override string ErrorCode => "NOT_FOUND";
}

public class ValidationApiException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
    public override string ErrorCode => "VALIDATION_ERROR";
}

public class UnprocessableApiException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
    public override string ErrorCode => "UNPROCESSABLE";
}

public class ConflictApiException(string message) : ApiException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public override string ErrorCode => "CONFLICT";
}
