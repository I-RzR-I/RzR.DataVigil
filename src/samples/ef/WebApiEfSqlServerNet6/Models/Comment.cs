using RzR.DataVigil.Abstractions.Contracts;
using System;
using System.Text.Json.Serialization;

namespace WebApiEfSqlServerNet6.Models
{
    public class Comment : IAuditable
    {
        public Guid Id { get; set; }

        public string Content { get; set; }

        public string Author { get; set; }

        public DateTime CreatedAt { get; set; }

        public Guid PostId { get; set; }

        [JsonIgnore]
        public Post Post { get; set; }
    }
}
