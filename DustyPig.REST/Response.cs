using System;
using System.Net;

namespace DustyPig.REST;

public class Response
{
    public bool Success { get; set; }

    public HttpStatusCode? StatusCode { get; set; }

    public string ReasonPhrase { get; set; }

    public string RawContent { get; set; }

    public Exception Error { get; set; }

    public void ThrowIfError()
    {
        if (Success)
            return;

        if (Error != null)
            throw new RestException(StatusCode, ReasonPhrase, RawContent, Error);

        string errorMsg = string.IsNullOrWhiteSpace(ReasonPhrase) ? "Unknown Error" : ReasonPhrase;
        throw new RestException(null, null, null, new Exception(errorMsg));
    }
}

public class Response<T> : Response
{
    public T Data { get; set; }
}
