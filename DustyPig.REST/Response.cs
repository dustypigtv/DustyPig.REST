using System;

namespace DustyPig.REST
{
    public class Response
    {
        public bool Success { get; set; }

        public Exception Error { get; set; }

        public void ThrowIfError()
        {
            if (!Success)
                throw Error;
        }
    }

    public class Response<T> : Response
    {
        public T Data { get; set; }
    }
}
