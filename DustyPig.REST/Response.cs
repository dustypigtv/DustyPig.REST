using System;
using System.Net;

namespace DustyPig.REST;

public class Response
{
    public bool Success { get; set; }

    public HttpStatusCode? StatusCode { get; set; }

    public string? ReasonPhrase { get; set; }

    public string? RawContent { get; set; }

    public Exception? Error { get; set; }

    public void ThrowIfError()
    {
        if (Success)
            return;

        throw new RestException(StatusCode, ReasonPhrase, RawContent, Error ?? new("Unknown Error"));
    }
}

public class Response<T> : Response
{
    public T? Data { get; set; }
}
