using ChapGiti.Application.Content;
using ChapGiti.Domain.Content;

namespace ChapGiti.Application.Features;

public sealed class MagazineService(IMagazineRepository repository, IUnitOfWork unitOfWork, SessionService sessions)
{
    private async Task Ready()
    {
        if (!await repository.Available())
            throw new UseCaseException(ErrorKind.Unavailable, "مجله پس از اجرای مهاجرت بررسی‌شده فعال می‌شود.", code: "MAGAZINE_UNAVAILABLE");
    }
    public async Task<MagazinePage> List(bool admin, string query, string status, string category, int page, int pageSize, bool oldestFirst = false)
    {
        await Ready();
        if (query.Length > 160 || category.Length > 60 || page is < 1 or > 100000 || pageSize is < 1 or > 50 || status != "" && status is not ("draft" or "published" or "archived"))
            throw new UseCaseException(ErrorKind.Invalid, "فیلتر مجله معتبر نیست.");
        return await repository.List(admin, query.Trim(), admin ? status : "published", category.Trim(), page, pageSize, oldestFirst);
    }
    public async Task<UseCaseResult> Detail(string slug)
    {
        await Ready();
        var found = await repository.PublicArticle(slug) ?? throw new UseCaseException(ErrorKind.NotFound, "مقاله پیدا نشد.");
        return UseCaseResult.Json(new
        {
            article = found.Article,
            canonicalSlug = found.Article.Slug,
            isAlias = found.Alias,
            related = await repository.Related(found.Article.Id, found.Article.Category)
        });
    }
    public async Task<UseCaseResult> Sitemap()
    {
        await Ready();
        return UseCaseResult.Json(await repository.Sitemap());
    }
    private sealed record EditorialDraft(string Id,string Slug,string Title,string Category,string Excerpt,string Alt,string SeoTitle,string SeoDescription,string Content);
    public async Task<UseCaseResult> PrepareDrafts(Dictionary<string,object?> administrator)
    {
        await Ready();
        using var source = typeof(MagazineService).Assembly.GetManifestResourceStream("ChapGiti.MagazineDrafts.fa.json")
            ?? throw new InvalidOperationException("Missing editorial draft resource.");
        var drafts = System.Text.Json.JsonSerializer.Deserialize<EditorialDraft[]>(source, System.Text.Json.JsonSerializerOptions.Web)!;
        var inserted = await unitOfWork.Transaction(async () =>
        {
            await repository.LockSlugs();
            var count = 0;
            foreach (var draft in drafts)
            {
                if (await repository.Find(draft.Id) != null || await repository.SlugUsed(draft.Slug,draft.Id)) continue;
                ArticlePublication.ValidateContent(draft.Content);
                var now = Input.Now();
                var article = new MagazineArticle(draft.Id,draft.Slug,draft.Title,draft.Excerpt,draft.Content,draft.Category,"draft",
                    null,draft.Alt,null,null,now,now,draft.SeoTitle,draft.SeoDescription,1);
                if (!await repository.Save(article,null,null)) throw new UseCaseException(ErrorKind.Conflict,"ثبت پیش‌نویس هم‌زمان تغییر کرد.");
                count++;
            }
            if (count > 0) await sessions.Audit(administrator.Str("email"), "آماده‌سازی پیش‌نویس‌های راهنمای چاپ");
            return count;
        });
        return UseCaseResult.Json(new { inserted, skipped = drafts.Length - inserted });
    }
    public async Task<UseCaseResult> Edit(string id)
    {
        await Ready();
        return UseCaseResult.Json(await repository.Find(id) ?? throw new UseCaseException(ErrorKind.NotFound, "مقاله پیدا نشد."));
    }
    public async Task<UseCaseResult> Save(string? id, JsonObject body, Dictionary<string, object?> administrator)
    {
        await Ready();
        var articleId = id ?? Input.NewId();
        var title = Input.Text(body, "title", 0, 180);
        var slug = Input.Text(body, "slug", 1, 120);
        if (!Regex.IsMatch(slug, @"^[a-z0-9]+(?:-[a-z0-9]+)*$"))
            throw new UseCaseException(ErrorKind.Invalid, "نشانی مقاله باید از حروف کوچک انگلیسی، عدد و خط تیره باشد.", code: "ARTICLE_SLUG_INVALID");
        var excerpt = Input.Text(body, "excerpt", 0, 400);
        var text = Input.Text(body, "content", 0, 40000);
        var category = Input.Text(body, "category", 0, 60);
        var state = Input.Text(body, "status", 1, 16);
        var cover = Input.Text(body, "coverAssetId", 0, 36);
        var alt = Input.Text(body, "coverAlt", 0, 250);
        var author = Input.Text(body, "authorName", 0, 120);
        var seoTitle = Input.Text(body, "seoTitle", 0, 100);
        var seoDescription = Input.Text(body, "seoDescription", 0, 180);
        if (cover != "" && !Guid.TryParse(cover, out _))
            throw new UseCaseException(ErrorKind.Invalid, "تصویر شاخص معتبر نیست.");
        ArticlePublication.ValidateContent(text);
        await unitOfWork.Transaction(async () =>
        {
            await repository.LockSlugs();
            var old = id == null ? null : await repository.Find(id);
            if (id != null && old == null)
                throw new UseCaseException(ErrorKind.NotFound, "مقاله پیدا نشد.");
            var expected = old == null ? (int?)null : (int)Input.Number(body, "version", 1, int.MaxValue);
            if (old != null && old.Version != expected)
                throw new UseCaseException(ErrorKind.Conflict, "مقاله توسط کاربر دیگری تغییر کرده؛ دوباره بارگذاری کنید.", code: "ARTICLE_VERSION_CONFLICT");
            ArticlePublication.Transition(old?.Status, state);
            if (state == "published")
                ArticlePublication.ValidatePublished(title, excerpt, text, category, cover == "" ? null : cover, alt);
            if (await repository.SlugUsed(slug, articleId))
                throw new UseCaseException(ErrorKind.Conflict, "این نشانی یا نشانی قدیمی آن قبلاً استفاده شده است.", code: "ARTICLE_SLUG_EXISTS");
            if (cover != "" && !await repository.AssetExists(cover))
                throw new UseCaseException(ErrorKind.Invalid, "تصویر شاخص موجود نیست.");
            var now = Input.Now();
            // Historical published dates may be unknown: ordinary edits must not invent one.
            var published = old?.PublishedAt ?? (state == "published" && old?.Status != "published" ? now : null);
            if (old != null && !body.ContainsKey("seoTitle")) seoTitle = old.SeoTitle;
            if (old != null && !body.ContainsKey("seoDescription")) seoDescription = old.SeoDescription;
            var article = new MagazineArticle(articleId, slug, title, excerpt, text, category, state, cover == "" ? null : cover, alt, author == "" ? null : author, published, old?.CreatedAt ?? now, now, seoTitle, seoDescription, (expected ?? 0) + 1);
            if (!await repository.Save(article, expected, old?.Slug))
                throw new UseCaseException(ErrorKind.Conflict, "مقاله هم‌زمان تغییر کرده است.", code: "ARTICLE_VERSION_CONFLICT");
            await sessions.Audit(administrator.Str("email"), "ذخیره مقاله «" + title + "» با وضعیت " + state);
            return true;
        });
        return UseCaseResult.Json(new
        {
            id = articleId,
            ok = true
        }, created: id == null);
    }
}
