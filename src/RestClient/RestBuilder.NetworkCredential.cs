// <summary>
// 
// The MIT License (MIT)
// 
// Copyright (c) 2020 Federico Mazzanti
// 
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
// 
// </summary>

namespace Mafesoft.RestClient
{
    using System;
    using System.Net;

    /// <summary>
    /// The network credentials of the requests
    /// </summary>
    public sealed partial class RestBuilder
    {
        /// <summary>
        /// Authentication information used by this func
        /// </summary>
        /// <param name="credential"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(Func<ICredentials?> credential)
        {
            if (credential == null) { throw new ArgumentNullException(nameof(credential)); }
            return With(r => r.credentials = credential());
        }

        /// <summary>
        /// Authentication information used by username and password
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, string password)
            => With(r => r.credentials = new NetworkCredential(username, password));

        /// <summary>
        /// Authentication information used by username, password and domain
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, string password, string domain)
            => With(r => r.credentials = new NetworkCredential(username, password, domain));

        /// <summary>
        /// Authentication information used by username and secure string password
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, System.Security.SecureString password)
            => With(r => r.credentials = new NetworkCredential(username, password));

        /// <summary>
        /// Authentication information used by username, secure string password and domain
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        public RestBuilder NetworkCredential(string username, System.Security.SecureString password, string domain)
            => With(r => r.credentials = new NetworkCredential(username, password, domain));
    }
}
