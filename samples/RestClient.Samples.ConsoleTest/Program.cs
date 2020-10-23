namespace RestClient.Samples.ConsoleTest
{
    using RestClient.Samples.NetworkLayer;
    using System;

    class Program
    {
        static void Main(string[] args)
        {
            NetworkService client = new NetworkService();

            var t = client.RootUsers().Parameter("Key", null).Get();

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
