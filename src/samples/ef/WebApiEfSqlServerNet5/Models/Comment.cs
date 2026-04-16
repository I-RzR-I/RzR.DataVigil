using System;
using System.Text.Json.Serialization;
using RzR.DataVigil.Abstractions.Contracts;

namespace WebApiEfSqlServerNet5.Models
{
    public class Comment : IAuditable
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        [JsonIgnore]
        public Post Post { get; set; }
        public string Author { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
