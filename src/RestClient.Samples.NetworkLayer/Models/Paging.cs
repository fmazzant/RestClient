namespace RestClient.Samples.NetworkLayer.Models
{
    public class Paging<T>
    {
        public int page { get; set; }
        public int per_page { get; set; }
        public int total { get; set; }
        public int total_pages { get; set; }
        public T[] data { get; set; }
        public Ad ad { get; set; }
    }
}
