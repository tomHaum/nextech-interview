using FluentAssertions;
using Nextech.Api.HackerNews;
using Nextech.Api.Models;

namespace Nextech.Api.UnitTests;

public class StoryCacheTests
{
    private static Story S(int id, string title) => new(id, title, $"https://x/{id}", "u", 0, 0);

    [Fact]
    public void Empty_cache_returns_empty_response()
    {
        var sut = new StoryCache();

        var result = sut.Query(null, page: 1, pageSize: 20);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public void Set_then_query_returns_first_page()
    {
        var sut = new StoryCache();
        sut.Set(Enumerable.Range(1, 25).Select(i => S(i, $"Title {i}")).ToList());

        var result = sut.Query(null, page: 1, pageSize: 10);

        result.Items.Should().HaveCount(10);
        result.Items.First().Id.Should().Be(1);
        result.Total.Should().Be(25);
    }

    [Fact]
    public void Query_returns_correct_slice_for_middle_page()
    {
        var sut = new StoryCache();
        sut.Set(Enumerable.Range(1, 25).Select(i => S(i, $"Title {i}")).ToList());

        var result = sut.Query(null, page: 2, pageSize: 10);

        result.Items.Select(s => s.Id).Should().Equal(11, 12, 13, 14, 15, 16, 17, 18, 19, 20);
    }

    [Fact]
    public void Query_returns_partial_last_page()
    {
        var sut = new StoryCache();
        sut.Set(Enumerable.Range(1, 25).Select(i => S(i, $"Title {i}")).ToList());

        var result = sut.Query(null, page: 3, pageSize: 10);

        result.Items.Should().HaveCount(5);
        result.Total.Should().Be(25);
    }

    [Fact]
    public void Query_beyond_last_page_returns_empty_with_correct_total()
    {
        var sut = new StoryCache();
        sut.Set(Enumerable.Range(1, 25).Select(i => S(i, $"Title {i}")).ToList());

        var result = sut.Query(null, page: 99, pageSize: 10);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(25);
    }

    [Fact]
    public void Search_is_case_insensitive_substring_on_title()
    {
        var sut = new StoryCache();
        sut.Set(new[] { S(1, "Foo Bar"), S(2, "FOO baz"), S(3, "Other") });

        var result = sut.Query("foo", page: 1, pageSize: 20);

        result.Items.Select(s => s.Id).Should().BeEquivalentTo(new[] { 1, 2 });
        result.Total.Should().Be(2);
    }

    [Fact]
    public void Search_total_reflects_filtered_count_not_cache_size()
    {
        var sut = new StoryCache();
        sut.Set(new[] { S(1, "alpha"), S(2, "beta"), S(3, "gamma") });

        var result = sut.Query("alpha", page: 1, pageSize: 20);

        result.Total.Should().Be(1);
    }

    [Fact]
    public void Set_replaces_previous_contents_atomically()
    {
        var sut = new StoryCache();
        sut.Set(new[] { S(1, "old") });
        sut.Set(new[] { S(2, "new") });

        sut.Count.Should().Be(1);
        sut.Query(null, 1, 10).Items.Single().Title.Should().Be("new");
    }
}
