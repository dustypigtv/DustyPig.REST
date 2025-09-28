using System;
using System.Net;

namespace DustyPig.REST;

public class RestException : Exception
{
    internal RestException(HttpStatusCode? statusCode, string? reasonPhrase, string? rawContent, Exception ex) : base(ex.Message, ex)
    {
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        RawContent = rawContent;
    }

    public HttpStatusCode? StatusCode { get; }

    public string? ReasonPhrase { get; }

    public string? RawContent { get; }
}