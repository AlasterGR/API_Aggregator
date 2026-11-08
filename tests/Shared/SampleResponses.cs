namespace GameDataAggregator.Tests.Shared;

/// <summary>Trimmed-down copies of real responses from the external APIs, used as test fixtures.</summary>
internal static class SampleResponses
{
    public const string RawgGames = """
        {
          "count": 2,
          "next": null,
          "previous": null,
          "results": [
            {
              "id": 3328,
              "slug": "the-witcher-3-wild-hunt",
              "name": "The Witcher 3: Wild Hunt",
              "released": "2015-05-18",
              "background_image": "https://media.rawg.io/media/games/618/witcher3.jpg",
              "rating": 4.65,
              "ratings_count": 6500,
              "metacritic": 92,
              "playtime": 45,
              "genres": [ { "id": 4, "name": "Action", "slug": "action" }, { "id": 5, "name": "RPG", "slug": "role-playing-games-rpg" } ],
              "platforms": [ { "platform": { "id": 4, "name": "PC", "slug": "pc" } }, { "platform": { "id": 187, "name": "PlayStation 5", "slug": "playstation5" } } ]
            },
            {
              "id": 10035,
              "slug": "the-witcher-4",
              "name": "The Witcher 4",
              "released": null,
              "background_image": null,
              "rating": 0,
              "ratings_count": 0,
              "metacritic": null,
              "playtime": 0,
              "genres": [],
              "platforms": null
            }
          ]
        }
        """;

    public const string CheapSharkDeals = """
        [
          {
            "internalName": "THEWITCHER3WILDHUNT",
            "title": "The Witcher 3: Wild Hunt",
            "metacriticLink": "/game/the-witcher-3-wild-hunt/",
            "dealID": "abc%2F123%3D",
            "storeID": "1",
            "gameID": "112330",
            "salePrice": "9.99",
            "normalPrice": "39.99",
            "isOnSale": "1",
            "savings": "75.018755",
            "metacriticScore": "93",
            "steamRatingText": "Overwhelmingly Positive",
            "steamRatingPercent": "96",
            "steamRatingCount": "700000",
            "steamAppID": "292030",
            "releaseDate": 1431907200,
            "lastChange": 1793867755,
            "dealRating": "9.6",
            "thumb": "https://shared.fastly.steamstatic.com/witcher3.jpg"
          },
          {
            "internalName": "REIGNSTHEWITCHER",
            "title": "Reigns: The Witcher",
            "metacriticLink": null,
            "dealID": "def456",
            "storeID": "25",
            "gameID": "316791",
            "salePrice": "5.99",
            "normalPrice": "5.99",
            "isOnSale": "0",
            "savings": "0.000000",
            "metacriticScore": "0",
            "steamRatingText": null,
            "steamRatingPercent": "0",
            "steamRatingCount": "0",
            "steamAppID": null,
            "releaseDate": 0,
            "lastChange": 1771977600,
            "dealRating": "0.0",
            "thumb": "https://shared.fastly.steamstatic.com/reigns.jpg"
          }
        ]
        """;

    public const string CheapSharkStores = """
        [
          { "storeID": "1", "storeName": "Steam", "isActive": 1, "images": { "banner": "/img/stores/banners/0.png" } },
          { "storeID": "25", "storeName": "Epic Games Store", "isActive": 1, "images": { "banner": "/img/stores/banners/24.png" } }
        ]
        """;

    public const string SteamStoreSearch = """
        {
          "total": 3,
          "items": [
            {
              "type": "app",
              "name": "The Witcher 3: Wild Hunt",
              "id": 292030,
              "price": { "currency": "USD", "initial": 4999, "final": 1249 },
              "tiny_image": "https://shared.akamai.steamstatic.com/292030.jpg",
              "metascore": "93",
              "platforms": { "windows": true, "mac": false, "linux": false },
              "streamingvideo": false
            },
            {
              "type": "bundle",
              "name": "The Witcher Trilogy Bundle",
              "id": 9999,
              "tiny_image": "https://shared.akamai.steamstatic.com/9999.jpg",
              "metascore": "",
              "platforms": { "windows": true, "mac": false, "linux": false },
              "streamingvideo": false
            },
            {
              "type": "app",
              "name": "The Witcher 2: Assassins of Kings Enhanced Edition",
              "id": 20920,
              "tiny_image": "https://shared.akamai.steamstatic.com/20920.jpg",
              "metascore": "",
              "platforms": { "windows": true, "mac": true, "linux": true },
              "streamingvideo": false
            }
          ]
        }
        """;

    public const string SteamPlayerCountWitcher3 = """{"response":{"player_count":29765,"result":1}}""";

    /// <summary>Steam answers 404 with this body for apps that have no player statistics.</summary>
    public const string SteamPlayerCountNotFound = """{"response":{"result":42}}""";

    /// <summary>Response of POST https://api.igdb.com/v4/games for the fields requested by the IGDB provider.</summary>
    public const string IgdbGames = """
        [
          {
            "id": 1942,
            "name": "The Witcher 3: Wild Hunt",
            "slug": "the-witcher-3-wild-hunt",
            "url": "https://www.igdb.com/games/the-witcher-3-wild-hunt",
            "summary": "RPG and sequel to The Witcher 2.",
            "first_release_date": 1431993600,
            "total_rating": 93.4567,
            "total_rating_count": 3120,
            "aggregated_rating": 92.71,
            "hypes": 120,
            "cover": { "id": 89386, "image_id": "co1wyy" },
            "genres": [ { "id": 12, "name": "Role-playing (RPG)" }, { "id": 31, "name": "Adventure" } ],
            "themes": [ { "id": 1, "name": "Action" }, { "id": 17, "name": "Fantasy" } ],
            "platforms": [ { "id": 6, "name": "PC (Microsoft Windows)" }, { "id": 48, "name": "PlayStation 4" } ],
            "involved_companies": [
              { "id": 1, "company": { "id": 908, "name": "CD Projekt RED" }, "developer": true, "publisher": false },
              { "id": 2, "company": { "id": 3, "name": "CD Projekt" }, "developer": false, "publisher": true },
              { "id": 3, "company": { "id": 70, "name": "Bandai Namco" }, "developer": false, "publisher": true }
            ]
          },
          {
            "id": 250616,
            "name": "The Witcher IV",
            "slug": "the-witcher-iv"
          }
        ]
        """;

    /// <summary>Response of POST https://id.twitch.tv/oauth2/token (client credentials flow).</summary>
    public const string TwitchToken = """{"access_token":"test-access-token","expires_in":5011271,"token_type":"bearer"}""";

    /// <summary>
    /// Response of GET https://www.speedrun.com/api/v1/games?name=witcher&amp;embed=platforms,genres,developers,publishers,categories.
    /// The second game has only the fields speedrun.com always returns.
    /// </summary>
    public const string SpeedrunGames = """
        {
          "data": [
            {
              "id": "k6qre01g",
              "names": { "international": "The Witcher 3: Wild Hunt", "japanese": null, "twitch": "The Witcher 3: Wild Hunt" },
              "abbreviation": "the_witcher_3_wild_hunt",
              "weblink": "https://www.speedrun.com/the_witcher_3_wild_hunt",
              "released": 2015,
              "release-date": "2015-05-18",
              "moderators": { "8dpnykg8": "super-moderator", "x7q0nq8p": "moderator", "j4r6pwm8": "moderator" },
              "assets": { "cover-large": { "uri": "https://www.speedrun.com/static/game/k6qre01g/cover?v=baba761" } },
              "platforms": { "data": [ { "id": "8gej2n93", "name": "PC" }, { "id": "nzelkr6q", "name": "PlayStation 4" } ] },
              "genres": { "data": [ { "id": "qdnqyk8e", "name": "Action RPG" } ] },
              "developers": { "data": [ { "id": "w7e6gj3m", "name": "CD Projekt RED" } ] },
              "publishers": { "data": [] },
              "categories": {
                "data": [
                  { "id": "02qr00pk", "name": "Any%", "type": "per-game", "miscellaneous": false },
                  { "id": "824xzvmd", "name": "100%", "type": "per-game", "miscellaneous": false },
                  { "id": "9d8pgl6k", "name": "Gwent", "type": "per-game", "miscellaneous": true },
                  { "id": "xd17ve8d", "name": "White Orchard", "type": "per-level", "miscellaneous": false }
                ]
              }
            },
            {
              "id": "46w5nn6r",
              "names": { "international": "The Witcher" },
              "release-date": null,
              "moderators": {},
              "platforms": { "data": [] },
              "genres": { "data": [] },
              "developers": { "data": [] },
              "publishers": { "data": [] },
              "categories": { "data": [] }
            }
          ],
          "pagination": { "offset": 0, "max": 10, "size": 2, "links": [] }
        }
        """;
}
