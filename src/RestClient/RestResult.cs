/// <summary>
/// 
/// The MIT License (MIT)
/// 
/// Copyright (c) 2020 Federico Mazzanti
/// 
/// Permission is hereby granted, free of charge, to any person
/// obtaining a copy of this software and associated documentation
/// files (the "Software"), to deal in the Software without
/// restriction, including without limitation the rights to use,
/// copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the
/// Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
/// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
/// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
/// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
/// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
/// OTHER DEALINGS IN THE SOFTWARE.
/// 
/// </summary>

namespace RestClient
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;

    /// <summary>
    /// Provides custom formatting for generic action result.
    /// </summary>
    public interface IRestResult
    {
        /// <summary>
        /// Gets content
        /// </summary>
        object Content { get; }
    }

    /// <summary>
    /// Provides custom formatting for generic action result with type.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the parameter of the method that this delegate encapsulates.This
    /// type parameter is contravariant. That is, you can use either the type you specified
    /// or any type that is less derived. For more information about covariance and contravariance,
    /// see Covariance and Contravariance in Generics.
    /// </typeparam>
    public interface IRestResult<T>
    {
        /// <summary>
        /// Gets content typed
        /// </summary>
        T Content { get; }
    }

    /// <summary>
    /// Provides a generic actionresult of a post/get/put/delete call request. This is an abstract class.
    /// </summary>
    public abstract class RestResult : IDisposable, IRestResult
    {
        #region [ Private ]
        /// <summary>
        /// Internal exception
        /// </summary>
        private Exception _InnerException;
        #endregion

        #region [ Constructors ]
        /// <summary>
        /// Initializes a new instance of the VarGroup.Mobile.Core.Net.ActionResult class.
        /// </summary>
        protected RestResult() : base() { }

        /// <summary>
        /// Initializes a new instance of the VarGroup.Mobile.Core.Net.ActionResult class.
        /// </summary>
        /// <param name="response">The HTTP response.</param>
        protected RestResult(HttpResponseMessage response)
        {
            this.Response = response;
        }

        /// <summary>
        /// Initializes a new instance of the VarGroup.Mobile.Core.Net.ActionResult class.
        /// </summary>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception. If the innerException
        /// parameter is not null, the current exception is raised in a catch block that
        /// handles the inner exception.
        /// </param>
        protected RestResult(Exception innerException)
        {
            _InnerException = innerException;
        }
        #endregion

        #region [ Public Properties ]
        /// <summary>
        /// Gets HTTP response
        /// </summary>
        public HttpResponseMessage Response { get; protected set; }

        /// <summary>
        /// Gets HTTP response's headers
        /// </summary>
        public HttpResponseHeaders Headers
        {
            get
            {
                if (this.Response != null)
                    return Response.Headers;
                return null;
            }
        }

        /// <summary>
        /// Gets the status code of the HTTP response was success
        /// </summary>
        public bool IsSuccessStatusCode
        {
            get
            {
                if (this.Response != null)
                    return Response.IsSuccessStatusCode;
                return false;
            }
        }

        /// <summary>
        /// Gets the status code of the HTTP response.
        /// </summary>
        public HttpStatusCode StatusCode
        {
            get
            {
                if (this.Response != null)
                    return Response.StatusCode;
                return HttpStatusCode.NotFound;
            }
        }

        /// <summary>
        /// Gets HTTP response's reason phrase
        /// </summary>
        public string ReasonPhrase
        {
            get
            {
                if (this.Response != null)
                    return Response.ReasonPhrase;
                return _InnerException.StackTrace;
            }
        }

        /// <summary>
        /// Gets HTTP response's version
        /// </summary>
        public Version Version
        {
            get
            {
                if (this.Response != null)
                    return Response.Version;
                return new Version("-");
            }
        }

        /// <summary>
        /// Gets HTTP response's content
        /// </summary>
        public object Content { get; internal set; }
        #endregion

        #region [ IDisposable ]
        /// <summary>
        /// True when the object was disposed.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Releases all resources used by the VarGroup.Mobile.Core.Net.ActionResult.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the VarGroup.Mobile.Core.Net.ActionResult and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.Response.Dispose();
                }
                disposed = true;
            }
        }
        #endregion
    }

    namespace Generic
    {
        /// <summary>
        /// Provides a specific T type action result of a post/get/put/delete call request. This is extends ActionResult.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the parameter of the method that this delegate encapsulates.This
        /// type parameter is contravariant. That is, you can use either the type you specified
        /// or any type that is less derived. For more information about covariance and contravariance,
        /// see Covariance and Contravariance in Generics.
        /// </typeparam>
        public class RestResult<T> : RestResult, IRestResult<T>
        {
            #region [ Constructors ]
            /// <summary>
            /// Initializes a new instance of the VarGroup.Mobile.Core.Net.ActionResult class.
            /// </summary>
            protected RestResult() : base() { }

            /// <summary>
            /// Initializes a new instance of the VarGroup.Mobile.Core.Net.ActionResult class.
            /// </summary>
            /// <param name="response">The HTTP response.</param>
            protected RestResult(HttpResponseMessage response) : base(response) { }

            /// <summary>
            /// Initializes a new instance of the VarGroup.Mobile.Core.Net.ActionResult class.
            /// </summary>
            /// <param name="innerException">
            /// The exception that is the cause of the current exception. If the innerException
            /// parameter is not null, the current exception is raised in a catch block that
            /// handles the inner exception.
            /// </param>
            protected RestResult(Exception innerException) : base(innerException) { Exception = innerException; }
            #endregion

            /// <summary>
            /// Gets HTTP response's generic T object content
            /// </summary>
            public new T Content { get; internal set; }

            /// <summary>
            /// Gets response's string content
            /// </summary>
            public string StringContent { get; internal set; }

            /// <summary>
            /// Get Exception during the result composing
            /// </summary>
            public Exception Exception { get; internal set; }

            /// <summary>
            /// Initializes a new instance of the VarGroup.Mobile.Core.Net.ActionResult from HTTP response message
            /// </summary>
            /// <typeparam name="TContent">
            /// The type of the parameter of the method that this delegate encapsulates.This
            /// type parameter is contravariant. That is, you can use either the type you specified
            /// or any type that is less derived. For more information about covariance and contravariance,
            /// see Covariance and Contravariance in Generics.
            /// </typeparam>
            /// <param name="response">HTTP response</param>
            /// <returns>An instance of ActionResult</returns>
            internal static RestResult<TContent> CreateInstanceFrom<TContent>(HttpResponseMessage response)
            {
                return new RestResult<TContent>(response);
            }

            /// <summary>
            /// Initializes a new instance of the VarGroup.Mobile.Core.Net.ActionResult from execption
            /// </summary>
            /// <param name="innerException">
            ///  The exception that is the cause of the current exception. If the innerException
            ///  parameter is not null, the current exception is raised in a catch block that
            ///  handles the inner exception.
            ///</param>
            /// <returns>An instance of ActionResult</returns>
            internal static RestResult<T> CreateFromException(Exception innerException)
            {
                return new RestResult<T>(innerException);
            }
        }
    }
}
