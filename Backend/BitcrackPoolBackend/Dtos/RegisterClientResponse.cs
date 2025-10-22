namespace BitcrackPoolBackend.Dtos
{
    public class RegisterClientResponse
    {
        public Guid ClientId { get; set; }
        public string ClientToken { get; set; } = string.Empty;
        public RangeDescriptor? AssignedRange { get; set; }
    }
}

