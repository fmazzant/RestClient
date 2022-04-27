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

namespace RestClient.Samples.ConsoleTest
{
    using RestClient.Samples.NetworkLayer;
    using System;

    static class Program
    {
        static void Main(string[] args)
        {
            NetworkService client = new NetworkService();

            Console.WriteLine("--- USERS ---");

            var usersList = client.GetUsersWithPaging(2);
            Console.WriteLine($"usersList.StatusCode:{(int)usersList.StatusCode}");

            var userFound = client.GetUserById(2);
            Console.WriteLine($"userFound.StatusCode:{(int)userFound.StatusCode}");

            var userNotFound = client.GetUserById(23);
            Console.WriteLine($"userNotFound.StatusCode:{(int)userNotFound.StatusCode}");

            var userCreate = client.CreateUser("Federico", "Developer");
            Console.WriteLine($"userCreate.StatusCode:{(int)userCreate.StatusCode}");

            var putUserUpdate = client.PutUpdateUser("Federico", "Developer");
            Console.WriteLine($"putUserUpdate.StatusCode:{(int)putUserUpdate.StatusCode}");

            var patchUserUpdate = client.PatchUpdateUser("Federico", "Developer");
            Console.WriteLine($"patchUserUpdate.StatusCode:{(int)patchUserUpdate.StatusCode}");

            var deleteUser = client.DeleteUser(2);
            Console.WriteLine($"deleteUser.StatusCode:{(int)deleteUser.StatusCode}");

            Console.WriteLine("--- RESOURCES ---");

            var resourceList = client.GetResourcesWithPaging("unknown", 2);
            Console.WriteLine($"resourceList.StatusCode:{(int)resourceList.StatusCode}");

            var resouceFound = client.GetResourceById(2, "unknown");
            Console.WriteLine($"resouceFound.StatusCode:{(int)resouceFound.StatusCode}");

            var resourceNotFound = client.GetResourceById(23, "unknown");
            Console.WriteLine($"resourceNotFound.StatusCode:{(int)resourceNotFound.StatusCode}");

            Console.WriteLine("--- REGISTERED ---");

            var register = client.Register("eve.holt@reqres.in", "pistol");
            Console.WriteLine($"register.StatusCode:{(int)register.StatusCode}");

            var registerUnSuccessfull = client.Register("eve.holt@reqres.in", null);
            Console.WriteLine($"registerUnSuccessfull.StatusCode:{(int)registerUnSuccessfull.StatusCode}");

            Console.WriteLine("--- LOGIN ---");

            var login = client.Register("eve.holt@reqres.in", "cityslicka");
            Console.WriteLine($"login.StatusCode:{(int)login.StatusCode}");

            var loginUnSuccessfull = client.Register("eve.holt@reqres.in", null);
            Console.WriteLine($"loginUnSuccessfull.StatusCode:{(int)loginUnSuccessfull.StatusCode}");

            Console.WriteLine("--- DELAYED RESPONSE ---");
            var delayedResponse = client.GetUsersWithDelay();
            Console.WriteLine($"delayedResponse.StatusCode:{(int)delayedResponse.StatusCode}");

            Console.WriteLine("press enter to exit.");
            Console.ReadLine();
        }
    }
}
