namespace LexCalculus.Web.Infrastructure.Rendering;

/// <summary>
/// Makale gövdesindeki inline görsel &lt;img&gt; etiketlerine responsive
/// srcset/sizes/loading attribute'ları ekler (Faz 6.8 #18). Variant dosyaları
/// (480/800/1200w .webp) yükleme sırasında üretilir; bu servis yalnızca disk'te
/// gerçekten var olan variant'ları srcset'e yazar. Variant yoksa (eski görseller)
/// img dokunulmaz → tarayıcı orijinal src'i kullanır (graceful degradation).
/// </summary>
public interface IImageVariantEnricher
{
    string Enrich(string? bodyHtml);
}
