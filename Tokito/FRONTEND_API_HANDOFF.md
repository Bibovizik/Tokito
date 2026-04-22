# Tokito Frontend API Handoff

## Base Rules
- Base URL in dev: `https://localhost:7046`
- Swagger: `https://localhost:7046/swagger`
- Auth is cookie-based, not JWT
- Frontend must send requests with credentials enabled
- Use `https`, not `http`, because auth cookie is `Secure`
- Dates use ISO format: `YYYY-MM-DD`
- `countryCode` is uppercase ISO-like code such as `UA`, `US`, `PL`

## Auth Notes
- After `POST /api/user/login`, the backend sets auth cookie `Tokito-Identity`
- `GET /api/games` and `GET /api/games/{id}` use query `countryCode` only for anonymous users
- For authenticated users, pricing country is taken from the auth cookie claim
- `GET /api/games/library` always uses the authenticated user's country

## Shared Response Shapes

### `GamePriceDto`
```json
{
  "amount": 799,
  "currencyCode": "UAH",
  "currencySymbol": "₴",
  "source": "BasePrice"
}
```

### `GameViewDTO`
```json
{
  "gameId": 1,
  "name": "QA Game",
  "releaseDate": "2026-04-22",
  "rating": 8,
  "publisherId": 2,
  "systemRequirements": {},
  "mostOneTimePlayers": 1,
  "publisherName": "Tokito QA Studio",
  "description": "Game description",
  "genres": [
    { "name": "Action", "description": "..." }
  ],
  "imageUrl": "https://example.com/game.jpg",
  "gameReviews": [
    {
      "userId": 5,
      "score": 8,
      "review": "Nice",
      "ratedAt": "2026-04-22T10:00:00Z"
    }
  ],
  "basePriceUah": 799,
  "currentPrice": {
    "amount": 19.99,
    "currencyCode": "USD",
    "currencySymbol": "$",
    "source": "RegionalPrice"
  },
  "walletPrice": {
    "amount": 19.99,
    "currencyCode": "USD",
    "currencySymbol": "$",
    "source": "WalletRegionalPrice"
  },
  "isOwnedByCurrentUser": false,
  "purchasedAt": "2026-04-22T11:15:00Z"
}
```

Notes:
- `gameReviews` is filled on `GET /api/games/{id}`
- `gameReviews` is empty on lists
- `purchasedAt` is mainly useful for library responses

## Auth / Account

### `GET /api/auth/status`
- Auth: optional cookie
- Request: no params
- Success `200` if logged in:
```json
{
  "id": "5",
  "email": "user@example.com",
  "username": "qa-user",
  "countryCode": "US",
  "publisherId": "2",
  "roles": ["User", "Publisher"]
}
```
- Error: `401` if anonymous

### `POST /api/auth/logout`
- Auth: required
- Request: no body
- Success: `200`
- Error: `401` if anonymous

### `POST /api/user/register`
- Auth: anonymous
- Request body:
```json
{
  "userNickname": "qa-user",
  "email": "qa-user@example.com",
  "password": "Passw0rd!",
  "countryCode": "US"
}
```
- Success: `200`
- Error: `400` with identity validation errors

### `POST /api/user/register-publisher`
- Auth: anonymous
- Request body:
```json
{
  "userNickname": "qa-publisher",
  "email": "qa-publisher@example.com",
  "password": "Passw0rd!",
  "countryCode": "UA",
  "publisherName": "Tokito QA Studio",
  "foundationDate": "2020-01-01",
  "website": "https://example.com"
}
```
- Success: `200`
- Error: `400` with validation/identity errors

### `POST /api/user/login`
- Auth: anonymous
- Request body:
```json
{
  "email": "qa-user@example.com",
  "password": "Passw0rd!"
}
```
- Success `200`:
```json
{
  "message": "Login successful"
}
```
- Errors:
  - `401` invalid email or password
  - `423` blocked account
  - `409` duplicate legacy users with same email

### `GET /api/user/profile`
- Auth: required
- Request: no params
- Success `200`:
```json
{
  "userId": 5,
  "userName": "qa-user",
  "userNickname": "qa-user",
  "email": "qa-user@example.com",
  "countryCode": "US",
  "registrationDate": "2026-04-22",
  "accountStatusCode": 1,
  "accountStatus": "Active",
  "publisherId": null,
  "publisherName": null,
  "roles": ["User"]
}
```
- Errors:
  - `401` invalid cookie/token
  - `404` user not found

### `POST /api/user/change-country`
- Auth: required
- Request body:
```json
{
  "countryCode": "UA",
  "currentCurrencyExchangeRateToUahSnapshot": 39.5,
  "newCurrencyExchangeRateToUahSnapshot": 1,
  "description": "Optional conversion comment"
}
```
- Required fields:
  - `countryCode`
- Optional fields:
  - `currentCurrencyExchangeRateToUahSnapshot`
  - `newCurrencyExchangeRateToUahSnapshot`
  - `description`
- Important:
  - exchange rates are needed only if wallet currency changes and user has non-zero balance
- Success `200`:
```json
{
  "previousCountryCode": "US",
  "countryCode": "UA",
  "previousCurrencyCode": "USD",
  "currencyCode": "UAH",
  "previousBalance": 100,
  "convertedBalance": 3950,
  "changedAt": "2026-04-22T12:00:00Z",
  "balanceWasConverted": true
}
```
- Errors:
  - `400` invalid country/rate payload
  - `404` user not found

### `PUT /api/user/{userId}/account-status`
- Auth: `Admin`
- Request body:
```json
{
  "accountStatus": 1
}
```
- Values:
  - `1` = Active
  - `2` = Blocked
- Success `200`:
```json
{
  "userId": 5,
  "accountStatus": 2
}
```
- Errors:
  - `400` invalid status
  - `404` user not found
  - `409` update conflict

### `DELETE /api/user/{userId}`
- Auth: `Admin`
- Request: no body
- Success `200`:
```json
{
  "userId": 5,
  "wasPublisherAccount": false,
  "publisherId": null
}
```
- Errors:
  - `404` user not found
  - `409` publisher still owns games

## Games / Store

### `GET /api/games`
- Auth: optional
- Query params:
  - `genre` optional exact-match genre name
  - `countryCode` optional, anonymous only, affects prices
- Success: `200` array of `GameViewDTO`

### `GET /api/games/{id}`
- Auth: optional
- Query params:
  - `countryCode` optional, anonymous only, affects prices
- Success: `200` single `GameViewDTO`
- Error: `404` game not found

### `GET /api/games/library`
- Auth: required
- Query params:
  - `genre` optional exact-match genre name
- Success: `200` array of purchased `GameViewDTO`
- Behavior:
  - returns only games owned by current user
  - `isOwnedByCurrentUser` is always `true`
  - `purchasedAt` is filled

### `POST /api/games`
- Auth: `Publisher`
- Request body:
```json
{
  "name": "QA Game",
  "releaseDate": "2026-04-22",
  "systemRequirements": {
    "os": "Windows 11",
    "ramGb": 16
  },
  "mostOneTimePlayers": 1,
  "description": "Endpoint test game",
  "basePriceUah": 799,
  "imageUrl": "https://example.com/game.jpg",
  "genreIds": [],
  "marketPriceOverrides": [
    {
      "marketCode": "US",
      "amount": 19.99
    }
  ]
}
```
- Notes:
  - `genreIds` may be `[]`
  - `marketPriceOverrides` may be `[]`
  - market override `UA` is not allowed
- Success `201`: `CreatedGameDto`
- `CreatedGameDto` contains:
  - `gameId`, `name`, `basePriceUah`, `publisherId`, `publisherName`, `releaseDate`, `systemRequirements`, `mostOneTimePlayers`, `description`, `imageUrl`, `genres[]`, `marketPrices[]`
- Errors:
  - `400` invalid payload / invalid genre ids / invalid market override
  - `404` publisher not found
  - `503` exchange rate service unavailable

### `PUT /api/games/{id}`
- Auth: `Publisher`
- Request body: same as create
- Note:
  - if `marketPriceOverrides` is `null`, existing manual overrides are preserved
- Success `200`: same shape as `CreatedGameDto`
- Errors:
  - `400` invalid payload
  - `403` user is not owner publisher
  - `404` game not found
  - `503` exchange rate service unavailable

### `DELETE /api/games`
- Auth: `Publisher` or `Admin`
- Request body is raw integer, not object:
```json
123
```
- Success `200`:
```json
{
  "message": "Deleted game with id: 123"
}
```
- Errors:
  - `403` not enough rights
  - `404` game not found
  - `400` other delete failure

### `POST /api/games/{gameId}/purchase`
- Auth: required
- Request: no body
- Success `200`:
```json
{
  "gameId": 1,
  "gameName": "QA Game",
  "purchasedAt": "2026-04-22T11:15:00Z",
  "chargedPrice": {
    "amount": 19.99,
    "currencyCode": "USD",
    "currencySymbol": "$",
    "source": "WalletRegionalPrice"
  },
  "remainingBalance": 80.01
}
```
- Errors:
  - `401` invalid user
  - `404` game not found
  - `400` insufficient funds
  - `409` already owned / purchase conflict / concurrency conflict

### `POST /api/games/createReview/{gameId}`
- Auth: `User`
- Request body:
```json
{
  "score": 8,
  "review": "Manual endpoint test"
}
```
- Rules:
  - `score` is `1..10`
  - one review per user per game
  - user must own the game
- Success: `200 "Success"`
- Errors:
  - `400` already reviewed or validation error
  - `403` user does not own the game
  - `404` game not found
  - `401` invalid user

### `GET /api/games/dashboard`
- Auth: `Publisher` or `Admin`
- Query params:
  - `dateFrom` optional `YYYY-MM-DD`
  - `dateTo` optional `YYYY-MM-DD`
  - `gameId` optional
  - `publisherId` optional, mainly for admin scope
- Success `200`:
```json
{
  "dateFrom": "2026-04-16",
  "dateTo": "2026-04-22",
  "publisherId": 2,
  "gameId": null,
  "totals": {
    "revenueUah": 10000,
    "copiesSold": 25,
    "gameCount": 3
  },
  "games": [
    {
      "gameId": 1,
      "gameName": "QA Game",
      "revenueUah": 7000,
      "copiesSold": 18
    }
  ],
  "daily": [
    {
      "date": "2026-04-22",
      "revenueUah": 1200,
      "copiesSold": 3
    }
  ],
  "adminOverview": {
    "totalUsers": 10,
    "activeUsers": 9,
    "blockedUsers": 1,
    "totalPublishers": 2,
    "totalGames": 5,
    "totalReviews": 7,
    "totalPurchases": 12,
    "totalLibraryEntries": 12
  }
}
```
- Notes:
  - `adminOverview` is present only for admins
  - for publishers, scope is limited to own publisher
- Errors:
  - `400` invalid date range
  - `403` forbidden scope
  - `404` game or publisher not found

## Wallet

### `GET /api/wallet`
- Auth: required
- Request: no params
- Success `200`:
```json
{
  "balances": [
    {
      "currencyCode": "USD",
      "availableAmount": 100
    }
  ],
  "entries": [
    {
      "walletEntryId": 1,
      "currencyCode": "USD",
      "amount": 100,
      "balanceAfter": 100,
      "entryType": "TopUp",
      "createdAt": "2026-04-22T11:00:00Z",
      "description": "QA top-up",
      "transactionId": null
    }
  ]
}
```
- Errors:
  - `401` invalid user

### `POST /api/wallet/top-up`
- Auth: required
- Request body:
```json
{
  "amount": 100,
  "exchangeRateToUahSnapshot": 39.5,
  "description": "QA top-up"
}
```
- Notes:
  - `amount` is credited in current wallet currency
  - `exchangeRateToUahSnapshot` is client-sent metadata, not fetched from NBU on this endpoint
- Success `200`:
```json
{
  "currencyCode": "USD",
  "creditedAmount": 100,
  "availableAmount": 100,
  "createdAt": "2026-04-22T11:00:00Z"
}
```
- Errors:
  - `400` invalid amount or invalid exchange rate
  - `401` invalid user

## Frontend Checklist
- Send credentials on every authenticated request
- After login, refresh session state with `GET /api/auth/status`
- Use `GET /api/user/profile` for profile header/basic account info
- Use `GET /api/games/library` for user library, not `GET /api/games`
- Use `GET /api/games/{id}` for full game page with reviews
- Expect review creation to fail with `403` if the user has not bought the game
- Expect admin dashboard to contain extra `adminOverview`
