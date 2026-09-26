namespace AuthDemo.Models;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, string Username, IEnumerable<string> Roles, DateTime ExpiresAt);

//public record ProductDto(int Id, string Name, decimal Price, string Category);