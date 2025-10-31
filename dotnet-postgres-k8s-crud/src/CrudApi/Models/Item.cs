namespace CrudApi.Models
{
    public class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool InStock { get; set; } = true;
    }

    public record ItemCreateDto(string Name, decimal Price, bool InStock);
    public record ItemUpdateDto(string Name, decimal Price, bool InStock);
}
