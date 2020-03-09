#pragma warning disable xUnit1013 // Public method should be marked as test
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using GWSuncoastAPI.Api;
using GWSuncoastAPI.Domain;
using IdentityModel.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using static JWT.Controllers.TokenController;


/// <summary>
/// Designed by AnaSoft Inc. 2019
/// http://www.anasoft.net/apincore
/// 
/// NOTE: Tests are not working with InMemory database.
///       Must update database connection in appsettings.json - "GWSuncoastAPIDB".  
///       Initial database and tables will be created and seeded once during tests startup
///
///
/// AUTHENTICATION:
/// This test drives which authentication/authorization mechanism is used.
/// Update appsettings.json to switch between 
/// "UseIndentityServer4": false = uses embeded JWT authentication 
/// "UseIndentityServer4": true  =  uses IndentityServer 4
/// IMPORTANT: Before run IS4 test must build the solution and run once solution with IndentityServer as startup project
///            After you get the start page for IndentityServer4 you can stop run and run unit tests 
/// </summary>

namespace GWSuncoastAPI.Test
{

    public class BaseTest
    {
        public static bool RemoteService = false;  //true to use service deployed to remote server
        public static string UserName = "my@email.com";
        public static string Password = "mysecretpassword123"; //encoded in User table
    }

    //https://xunit.net/docs/shared-context#collection-fixture
    /// <summary>
    /// Initialize Http client for testing for all test classes
    /// </summary>
    public class HttpClientFixture : IDisposable
    {
        public HttpClient Client { get; private set; }
        public HttpClientFixture()
        {
            var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
           .AddJsonFile("appsettings.json").Build();
            //overwrite if azure db test
            if (BaseTest.RemoteService)
            {
                configuration = new ConfigurationBuilder()
                   .SetBasePath(Directory.GetCurrentDirectory())
                           .AddJsonFile("appsettings-remote.json").Build();
            }

            IWebHostBuilder whb = new WebHostBuilder().UseStartup<Startup>();
            whb.UseConfiguration(configuration);
            var server = new TestServer(whb);
            Client = server.CreateClient();

        }
        public void Dispose()
        {
            Client.Dispose();
        }
    }

    [CollectionDefinition("HttpClient collection")]
    public class HttpClientCollection : ICollectionFixture<HttpClientFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class Token
    {
        public string token;
    }

    [Collection("HttpClient collection")]
    public class TokenTest : BaseTest
    {
        static HttpClient Client;
        public HttpClientFixture fixture;

        public TokenTest(HttpClientFixture fixture)
        {
            this.fixture = fixture;
            Client = fixture.Client;
        }

        public static string TokenValue { get; set; }


        /// <summary>
        /// This test drives which authentication/authorization mechanism is used.
        /// Update appsettings.json to switch between 
        /// "UseIndentityServer4": false = uses embeded JWT authentication 
        /// "UseIndentityServer4": true  =  uses IndentityServer 4
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task token_test()
        {
            await token_get(null);
        }
        public static async Task token_get(HttpClient client)
        {
            if (client == null)
                client = Client;
            if (!String.IsNullOrEmpty(TokenValue)) return;

            //read from tests settings
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json").Build();

            //JWT or IS4 authentication
            if (configuration["Authentication:UseIndentityServer4"] == "False")
            { //JWT
                LoginModel login = new LoginModel { Username = UserName, Password = Password };
                var response = await client.PostAsync("/api/token", new StringContent(JsonConvert.SerializeObject(login), Encoding.UTF8, "application/json"));
                response.EnsureSuccessStatusCode();
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var jsonString = await response.Content.ReadAsStringAsync();
                var token = JsonConvert.DeserializeObject<Token>(jsonString);
                TokenValue = token.token;
            }
            else
            { //IS4

                var is4ip = configuration["Authentication:IndentityServer4IP"];
                // discover endpoints from the metadata by calling Auth server hosted on 5000 port
                var discoveryClient = await DiscoveryClient.GetAsync(is4ip);
                if (discoveryClient.IsError)
                {
                    Console.WriteLine(discoveryClient.Error);
                    throw new Exception(discoveryClient.Error);
                }
                //// request the token from the Auth server for type ClientCredentials
                //var tokenClient1 = new TokenClient(discoveryClient.TokenEndpoint, "clientCred", "secret");
                //var response1 = await tokenClient1.RequestClientCredentialsAsync("GWSuncoastAPI");
                //var resp1 = response1.Json;


                //BAD client test
                var tokenClient = new TokenClient(discoveryClient.TokenEndpoint, "GWSuncoastAPIClient-BAD", "secret");
                var response = await tokenClient.RequestResourceOwnerPasswordAsync("my@email.com", "mysecretpassword123", "GWSuncoastAPI");
                var response_json = response.Json;
                if (response.IsError)
                {
                    Console.WriteLine(response.Error);
                    Console.WriteLine(response.ErrorDescription);
                }
                Assert.True(response.IsError);
                Assert.Equal("invalid_client", response.Error);
                Assert.Equal(HttpStatusCode.BadRequest, response.HttpStatusCode);

                //BAD grant test
                tokenClient = new TokenClient(discoveryClient.TokenEndpoint, "GWSuncoastAPIClient", "secret");
                response = await tokenClient.RequestResourceOwnerPasswordAsync("my@email.com", "mysecretpassword123-BAD", "GWSuncoastAPI");
                response_json = response.Json;
                if (response.IsError)
                {
                    Console.WriteLine(response.Error);
                    Console.WriteLine(response.ErrorDescription);
                }
                Assert.True(response.IsError);
                Assert.Equal("invalid_grant", response.Error);
                Assert.Equal(HttpStatusCode.BadRequest, response.HttpStatusCode);

                //GOOD TEST----------------
                //use your own user list (from database) to get a token for API user
                tokenClient = new TokenClient(discoveryClient.TokenEndpoint, "GWSuncoastAPIClient", "secret");
                response = await tokenClient.RequestResourceOwnerPasswordAsync(UserName, Password, "GWSuncoastAPI");
                response_json = response.Json;
                if (response.IsError)
                {
                    Console.WriteLine(response.Error);
                    Console.WriteLine(response.ErrorDescription);
                }
                Assert.False(response.IsError);
                Assert.Equal(HttpStatusCode.OK, response.HttpStatusCode);
                var jsonString = response.AccessToken;
                var token = new Token(); token.token = jsonString;
                TokenValue = token.token;
            }
        }

    }


    #region Account tests
    /// <summary>
    /// Account API Integration tests
    /// </summary>
    [Collection("HttpClient collection")]
    public class AccountTest : BaseTest
    {
        public HttpClientFixture fixture;
        public AccountTest(HttpClientFixture fixture)
        {
            this.fixture = fixture;
            var client = fixture.Client;
        }

        #region Account tests

        [Fact]
        public async Task account_getall()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/account");
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var accounts = (ICollection<AccountViewModel>)JsonConvert.DeserializeObject<IEnumerable<AccountViewModel>>(jsonString);
            Assert.True(accounts.Count > 0);
            //clean
            await util.removeAccount(client, accountid);
        }


        [Fact]
        public async Task account_add_update_delete()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);

            //insert
            AccountViewModel vmentity = new AccountViewModel
            {
                Name = "Account 1",
                Email = "apincore@anasoft.net",
                Description = "desc",
                IsTrial = false,
                IsActive = true,
                SetActive = DateTime.Now
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/account", new StringContent(
                                                               JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var lastAddedId = await response.Content.ReadAsStringAsync();
            Assert.True(int.Parse(lastAddedId) > 1);
            int id = 0; int.TryParse(lastAddedId, out id);

            //get inserted
            var util = new Utility();
            vmentity = await util.GetAccount(client, id);

            //update test
            vmentity.Description = "desc updated";
            response = await client.PutAsync("/api/account/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            //confirm update
            response = await client.GetAsync("/api/account/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var oj = JObject.Parse(jsonString);
            var desc = oj["description"].ToString();
            Assert.Equal(desc, vmentity.Description);

            //another update with same account - concurrency
            vmentity.Description = "desc updated 2";
            response = await client.PutAsync("/api/account/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

            //delete test 
            response = await client.DeleteAsync("/api/account/" + id.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        }

        [Fact]
        public async Task account_getbyid()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/account/" + accountid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            Assert.True(account.Name == "Account");
            //clean
            await util.removeAccount(client, accountid);
        }

        [Fact]
        public async Task account_getactivebyname()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            //get by id           
            var response = await client.GetAsync("/api/account/" + accountid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            //
            response = await client.GetAsync("/api/account/GetActiveByName/" + account.Name);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var accounts = (ICollection<AccountViewModel>)JsonConvert.DeserializeObject<IEnumerable<AccountViewModel>>(jsonString);
            Assert.True(accounts.Count > 0);
            //clean
            await util.removeAccount(client, accountid);
        }

        #endregion

        #region Account async tests

        [Fact]
        public async Task account_getallasync()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/accountasync");
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var accounts = (ICollection<AccountViewModel>)JsonConvert.DeserializeObject<IEnumerable<AccountViewModel>>(jsonString);
            Assert.True(accounts.Count > 0);
            //clean
            await util.removeAccount(client, accountid);
        }


        [Fact]
        public async Task account_add_update_delete_async()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);

            //insert
            AccountViewModel vmentity = new AccountViewModel
            {
                Name = "Account 1",
                Email = "apincore@anasoft.net",
                Description = "desc",
                IsTrial = false,
                IsActive = true,
                SetActive = DateTime.Now
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/accountasync", new StringContent(
                                                               JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var lastAddedId = await response.Content.ReadAsStringAsync();
            Assert.True(int.Parse(lastAddedId) > 1);
            int id = 0; int.TryParse(lastAddedId, out id);

            //get inserted
            var util = new Utility();
            vmentity = await util.GetAccount(client, id);

            //update test
            vmentity.Description = "desc updated";
            response = await client.PutAsync("/api/accountasync/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            //confirm update
            response = await client.GetAsync("/api/accountasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var oj = JObject.Parse(jsonString);
            var desc = oj["description"].ToString();
            Assert.Equal(desc, vmentity.Description);

            //another update with same account - concurrency
            vmentity.Description = "desc updated 2";
            response = await client.PutAsync("/api/accountasync/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

            //delete test 
            response = await client.DeleteAsync("/api/accountasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        }


        [Fact]
        public async Task account_getbyidasync()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/accountasync/" + accountid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            Assert.True(account.Name == "Account");
            //clean
            await util.removeAccount(client, accountid);
        }

        [Fact]
        public async Task account_getactivebynameasync()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            //get by id           
            var response = await client.GetAsync("/api/accountasync/" + accountid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            //
            response = await client.GetAsync("/api/accountasync/GetActiveByName/" + account.Name);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var accounts = (ICollection<AccountViewModel>)JsonConvert.DeserializeObject<IEnumerable<AccountViewModel>>(jsonString);
            Assert.True(accounts.Count > 0);
            //clean
            await util.removeAccount(client, accountid);
        }

        #endregion

    }
    #endregion

    #region User tests
    [Collection("HttpClient collection")]
    public class UserTest : BaseTest
    {
        public HttpClientFixture fixture;
        public UserTest(HttpClientFixture fixture)
        {
            this.fixture = fixture;
            var client = fixture.Client;
        }

        public static string LastAddedUser { get; set; }

        #region User tests

        [Fact]
        public async Task user_getall()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            var userid = await util.addUser(client, accountid);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/user");
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var users = (ICollection<UserViewModel>)JsonConvert.DeserializeObject<IEnumerable<UserViewModel>>(jsonString);
            Assert.True(users.Count > 0);
            //clean
            await util.removeUser(client, userid);
            await util.removeAccount(client, accountid);
        }


        [Fact]
        public async Task user_add_update_delete()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //insert
            UserViewModel vmentity = new UserViewModel
            {
                FirstName = "User 1",
                LastName = "LastName",
                Email = "apincore@anasoft.net",
                Description = "desc",
                IsAdminRole = true,
                IsActive = true,
                Password = " ",
                AccountId = 1
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/user", new StringContent(
                                                               JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var lastAddedId = await response.Content.ReadAsStringAsync();
            Assert.True(int.Parse(lastAddedId) > 1);
            int id = 0; int.TryParse(lastAddedId, out id);

            //get inserted
            var util = new Utility();
            vmentity = await util.GetUser(client, id);

            //update test
            vmentity.Description = "desc updated";
            response = await client.PutAsync("/api/user/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            //confirm update
            response = await client.GetAsync("/api/user/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var oj = JObject.Parse(jsonString);
            var desc = oj["description"].ToString();
            Assert.Equal(desc, vmentity.Description);

            //another update with same account - concurrency
            vmentity.Description = "desc updated 2";
            response = await client.PutAsync("/api/user/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

            //delete test 
            response = await client.DeleteAsync("/api/user/" + id.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        }

        [Fact]
        public async Task user_getbyid()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            var userid = await util.addUser(client, accountid);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/user/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            Assert.True(user.FirstName == "FirstName");
            // lazy-loading test
            response = await client.GetAsync("/api/account/" + accountid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            Assert.True(account.Users.Count == 1);
            //clean
            await util.removeUser(client, userid);
            await util.removeAccount(client, accountid);
        }

        [Fact]
        public async Task user_getactivebyfirstname()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            var userid = await util.addUser(client, accountid);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            //get by id           
            var response = await client.GetAsync("/api/user/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            //
            response = await client.GetAsync("/api/user/GetActiveByFirstName/" + user.FirstName);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var users = (ICollection<UserViewModel>)JsonConvert.DeserializeObject<IEnumerable<UserViewModel>>(jsonString);
            Assert.True(users.Count > 0);
            //clean
            await util.removeUser(client, userid);
            await util.removeAccount(client, accountid);
        }

        [Fact]
        public async Task user_getbyname_sp()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            var userid = await util.addUser(client, accountid);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            //get by id           
            var response = await client.GetAsync("/api/user/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            //
            response = await client.GetAsync("/api/user/GetUsersByName/" + user.FirstName + "/" + user.LastName);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var users = (ICollection<UserViewModel>)JsonConvert.DeserializeObject<IEnumerable<UserViewModel>>(jsonString);
            Assert.True(users.Count > 0);
            //clean
            await util.removeUser(client, userid);
            await util.removeAccount(client, accountid);
        }

        [Fact]
        public async Task user_updateemailbyusername_sp()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            var userid = await util.addUser(client, accountid);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            //get by id           
            var response = await client.GetAsync("/api/user/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            //
            string updatedEmail = "newemail@com.com";
            response = await client.GetAsync("/api/user/UpdateEmailbyUsername/" + user.UserName + "/" + updatedEmail);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var records = await response.Content.ReadAsStringAsync();
            Assert.True(records != "0");
            //get by id to confirm update           
            response = await client.GetAsync("/api/user/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            Assert.True(user.Email == updatedEmail);
            //clean
            await util.removeUser(client, userid);
            await util.removeAccount(client, accountid);
        }

        #endregion

        #region User async tests

        [Fact]
        public async Task user_getallasync()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            var userid = await util.addUser(client, accountid);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/userasync");
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var users = (ICollection<UserViewModel>)JsonConvert.DeserializeObject<IEnumerable<UserViewModel>>(jsonString);
            Assert.True(users.Count > 0);
            // lazy-loading test
            response = await client.GetAsync("/api/accountasync/" + accountid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            Assert.True(account.Users.Count == 1);
            //clean
            await util.removeUser(client, userid);
            await util.removeAccount(client, accountid);
        }


        [Fact]
        public async Task user_add_update_delete_async()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //insert
            UserViewModel vmentity = new UserViewModel
            {
                FirstName = "User 1",
                LastName = "LastName",
                Email = "apincore@anasoft.net",
                Description = "desc",
                IsAdminRole = true,
                IsActive = true,
                Password = " ",
                AccountId = 1
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/userasync", new StringContent(
                                                               JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var lastAddedId = await response.Content.ReadAsStringAsync();
            Assert.True(int.Parse(lastAddedId) > 1);
            int id = 0; int.TryParse(lastAddedId, out id);

            //get inserted
            var util = new Utility();
            vmentity = await util.GetUser(client, id);

            //update test
            vmentity.Description = "desc updated";
            response = await client.PutAsync("/api/userasync/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

            //confirm update
            response = await client.GetAsync("/api/userasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var oj = JObject.Parse(jsonString);
            var desc = oj["description"].ToString();
            Assert.Equal(desc, vmentity.Description);

            //another update with same account - concurrency
            vmentity.Description = "desc updated 2";
            response = await client.PutAsync("/api/userasync/" + id.ToString(), new StringContent(JsonConvert.SerializeObject(vmentity), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

            //delete test 
            response = await client.DeleteAsync("/api/userasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        }

        [Fact]
        public async Task user_getbyidasync()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            var userid = await util.addUser(client, accountid);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/userasync/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            Assert.True(user.FirstName == "FirstName");
            //clean
            await util.removeUser(client, userid);
            await util.removeAccount(client, accountid);
        }

        [Fact]
        public async Task user_getactivebyfirstnameasync()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            var userid = await util.addUser(client, accountid);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            //get by id           
            var response = await client.GetAsync("/api/userasync/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            //
            response = await client.GetAsync("/api/userasync/GetActiveByFirstName/" + user.FirstName);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var users = (ICollection<UserViewModel>)JsonConvert.DeserializeObject<IEnumerable<UserViewModel>>(jsonString);
            Assert.True(users.Count > 0);
            //clean
            await util.removeUser(client, userid);
            await util.removeAccount(client, accountid);
        }

        [Fact]
        public async Task user_getbynameasync_sp()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            var userid = await util.addUser(client, accountid);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            //get by id           
            var response = await client.GetAsync("/api/userasync/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            //
            response = await client.GetAsync("/api/userasync/GetUsersByName/" + user.FirstName + "/" + user.LastName);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            var users = (ICollection<UserViewModel>)JsonConvert.DeserializeObject<IEnumerable<UserViewModel>>(jsonString);
            Assert.True(users.Count > 0);
            //clean
            await util.removeUser(client, userid);
            await util.removeAccount(client, accountid);
        }

        [Fact]
        public async Task user_updateemailbyusernameasync_sp()
        {
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var util = new Utility();
            var accountid = await util.addAccount(client);
            var userid = await util.addUser(client, accountid);
            //
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            //get by id           
            var response = await client.GetAsync("/api/userasync/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            //
            string updatedEmail = "newemail@com.com";
            response = await client.GetAsync("/api/userasync/UpdateEmailbyUsername/" + user.UserName + "/" + updatedEmail);
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var records = await response.Content.ReadAsStringAsync();
            Assert.True(records != "0");
            //get by id to confirm update           
            response = await client.GetAsync("/api/userasync/" + userid.ToString());
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            jsonString = await response.Content.ReadAsStringAsync();
            user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            Assert.True(user.Email == updatedEmail);
            //clean
            await util.removeUser(client, userid);
            await util.removeAccount(client, accountid);
        }


        #endregion

    }

    #endregion


    #region Shared test

    public class Utility
    {
        public async Task<int> addAccount(HttpClient client)
        {
            AccountViewModel account = new AccountViewModel
            {
                Name = "Account",
                Email = "apincore@anasoft.net",
                Description = "desc" + (new Random()).Next().ToString(),
                IsTrial = false,
                IsActive = true,
                SetActive = DateTime.Now
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/accountasync", new StringContent(
                                                               JsonConvert.SerializeObject(account), Encoding.UTF8, "application/json"));
            var jsonString = await response.Content.ReadAsStringAsync();
            int lastAdded = 0;
            int.TryParse(jsonString, out lastAdded);
            return lastAdded;
        }
        public async Task<AccountViewModel> GetAccount(HttpClient client, int id)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/accountasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var account = JsonConvert.DeserializeObject<AccountViewModel>(jsonString);
            return account;
        }
        public async Task removeAccount(HttpClient client, int id)
        {
            await client.DeleteAsync("/api/account/" + id.ToString());
        }

        public async Task<int> addUser(HttpClient client, int accountId)
        {
            UserViewModel user = new UserViewModel
            {
                FirstName = "FirstName",
                LastName = "LastName",
                UserName = "username",
                Email = "email",
                Description = "desc" + (new Random()).Next().ToString(),
                IsAdminRole = true,
                IsActive = true,
                Password = " ",
                AccountId = accountId
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.PostAsync("/api/userasync", new StringContent(
                                                               JsonConvert.SerializeObject(user), Encoding.UTF8, "application/json"));
            var jsonString = await response.Content.ReadAsStringAsync();
            int lastAdded = 0;
            int.TryParse(jsonString, out lastAdded);
            return lastAdded;
        }
        public async Task<UserViewModel> GetUser(HttpClient client, int id)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenTest.TokenValue);
            var response = await client.GetAsync("/api/userasync/" + id.ToString());
            response.EnsureSuccessStatusCode();
            var jsonString = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<UserViewModel>(jsonString);
            return user;
        }
        public async Task removeUser(HttpClient client, int id)
        {
            await client.DeleteAsync("/api/user/" + id.ToString());
        }

    }
    #endregion


    #region async Load tests
    [Collection("HttpClient collection")]
    public class ZLoadTest : BaseTest
    {
        public HttpClientFixture fixture;
        public ZLoadTest(HttpClientFixture fixture)
        {
            this.fixture = fixture;
            var client = fixture.Client;
        }


        /// <summary>
        /// Load test
        /// --local service: BaseTest.RemoteService = false  
        /// --remote service: BaseTest.RemoteService = true   
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task LoadTest()
        {
            int loopmax = 10;
            var client = fixture.Client;
            if (String.IsNullOrEmpty(TokenTest.TokenValue)) await TokenTest.token_get(client);
            //
            var accountId = 0;
            var userId = 0;
            var util = new Utility();
            int i = 1;
            while (i < loopmax)
            {
                accountId = await util.addAccount(client);
                userId = await util.addUser(client, accountId);
                await util.GetAccount(client, accountId);
                await util.GetUser(client, userId);
                await util.removeUser(client, userId);
                await util.removeAccount(client, accountId);
                i++;
            }
            //
            Assert.True(i == loopmax);

        }

    }
    #endregion


}
