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

namespace RestClient.Samples.NetworkLayer
{
    using RestClient.Generic;
    using RestClient.Samples.NetworkLayer.Models;
    using System;
    using System.Threading.Tasks;

    public class NetworkService
    {
        protected RestBuilder Root() => Rest
            .Build((p) =>
            {
                p.EndPoint = new Uri("https://reqres.in/api");
            })
            .CertificateValidation((obj, certificate, clain, errors) => true); //for development mode

        public NetworkService()
        {

        }

        #region [ USERS ] 
        public RestBuilder RootUsers() => Root().Command("/users");
        public async Task<RestResult<Paging<User>>> GetUsersWithPagingAsync(int? page = null) => await RootUsers()
            .Parameter((p) =>
            {
                if (page.HasValue)
                {
                    p.Add(new RestParameter() { Key = "page", Value = page });
                }
            })
            .GetAsync<Paging<User>>();
        public RestResult<Paging<User>> GetUsersWithPaging(int? page = null) => GetUsersWithPagingAsync(page).Result;
        public async Task<RestResult<Single<User>>> GetUserByIdAsync(int id) => await RootUsers()
            .Command(id)
            .GetAsync<Single<User>>();
        public RestResult<Single<User>> GetUserById(int id) => GetUserByIdAsync(id).Result;
        public async Task<RestResult> CreateUserAsync(string name, string job) => await RootUsers()
            .Payload<dynamic>(new { name, job })
            .PostAsync();
        public RestResult CreateUser(string name, string job) => CreateUserAsync(name, job).Result;
        public async Task<RestResult> PutUpdateUserAsync(string name, string job) => await RootUsers()
          .Payload<dynamic>(new { name, job })
          .PutAsync();
        public RestResult PutUpdateUser(string name, string job) => PutUpdateUserAsync(name, job).Result;
        public async Task<RestResult> PatchUpdateUserAsync(string name, string job) => await RootUsers()
           .Payload<dynamic>(new { name, job })
           .PatchAsync();
        public RestResult PatchUpdateUser(string name, string job) => PatchUpdateUserAsync(name, job).Result;
        public async Task<RestResult> DeleteUserAsync(int id) => await RootUsers()
           .Command(id)
           .DeleteAsync();
        public RestResult DeleteUser(int id) => DeleteUserAsync(id).Result;

        #endregion

        #region  [ RESOURCES ]
        public RestBuilder RootResources(string resourceName = "unknown") => Root().Command(resourceName);
        public async Task<RestResult<Paging<Resource>>> GetResourcesWithPagingAsync(string name = null, int? page = null)
            => await RootResources(name)
           .Parameter((p) =>
           {
               if (page.HasValue)
               {
                   p.Add(new RestParameter() { Key = "page", Value = page });
               }
           })
           .GetAsync<Paging<Resource>>();
        public RestResult<Paging<Resource>> GetResourcesWithPaging(string name = null, int? page = null)
            => GetResourcesWithPagingAsync(name, page).Result;
        public async Task<RestResult<Single<Resource>>> GetResourceByIdAsync(int id, string name = null)
            => await RootResources(name)
            .Command(id)
            .GetAsync<Single<Resource>>();
        public RestResult<Single<Resource>> GetResourceById(int id, string name = null)
            => GetResourceByIdAsync(id, name).Result;
        #endregion

        #region [ REGISTER ]
        public RestBuilder RootRegister() => Root().Command("register");
        public async Task<RestResult> RegisterAsync(string email, string password) => await RootRegister()
           .Payload<dynamic>(new { email, password })
           .PostAsync();
        public RestResult Register(string email, string password) => RegisterAsync(email, password).Result;
        #endregion

        #region [ LOGIN ]
        public RestBuilder RootLogin() => Root().Command("login");
        public async Task<RestResult> LoginAsync(string email, string password) => await RootRegister()
           .Payload<dynamic>(new { email, password })
           .PostAsync();
        public RestResult Login(string email, string password) => LoginAsync(email, password).Result;
        #endregion

        #region [ DELAYED RESPONSE ]
        public async Task<RestResult> GetUsersWithDelayAsync() => await RootUsers()
            .Parameter("delay", "3")
            .GetAsync();
        public RestResult GetUsersWithDelay() => GetUsersWithDelayAsync().Result;
        #endregion
    }
}
