using System;


namespace Google_Like_Blazor.Data
{
	public class FileModel
	{
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string FileName { get; set; }
        public string Type { get; set; }
        public byte[] Content { get; set; }
    }
}

