using System;
using System.Collections.Generic;
using RzR.DataVigil.Abstractions.Contracts;

namespace WebApiEfPostgreSqlNet5.Models
{
    public class Post : IAuditable
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string Author { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
