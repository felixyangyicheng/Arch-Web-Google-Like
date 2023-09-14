using System;
using Google_Like_Blazor.Data;
using System.Runtime;
using Google_Like_Blazor.Services;
using MongoDB.Driver;

namespace Google_Like_Blazor.Test.Services
{
    [TestClass]

    public class FileServiceTest
	{
        private IFileRepo _fileRepo;

        [TestInitialize]
        public void TestInitialize()
        {

 
        var mongoClient = new MongoClient("mongodb://root:123456@localhost:27017");

        var mongoDatabase = mongoClient.GetDatabase(
                "google-like");

        var _collection = mongoDatabase.GetCollection<FileModel>(
                "files");
            _fileRepo = new FileService(_collection);
        }
        [TestCleanup]
        public void TestCleanup()
        {
            
        }

        [TestMethod]

        public async Task SearchWithKeyNotNull()
        {
            // Arrange
            string key= "sport";
            // Act
            var result = await _fileRepo.SearchInContent(key);
            // Assert
            Assert.IsNotNull(result);
    
        }


        [TestMethod]

        public async Task SearchWithWhitespaceReturn0()
        {
            // Arrange
            string key = " ";
            // Act
            var result = await _fileRepo.SearchInContent(key);
            // Assert
            Assert.IsTrue(result.Count()==7);

        }
    }

}

