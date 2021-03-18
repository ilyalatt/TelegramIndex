# Telegram Index

## Summary

Telegram `#whois` channel messages scraper and UI for browsing the scraped messages.

## Hosting

The easiest way is to use the docker-compose:
* Clone the repository (or download only `docker-compose.yaml`).
* Create `data` directory.
* Create `data/config.json`.
* Here is an example of `config.json`. All you need to change is `Scraper.ChannelUsername`.
```JSON
{
  "Telegram": {
    "ApiId": 17349,
    "ApiHash": "344583e45741c457fe1862106095a5eb"
  },
  "Scraper": {
    "ChannelUsername": "startupneversleeps"
  },
  "Trace": false,
  "DisableSync": false
}
```
* Run `docker-compose run app` (it is needed for authorization only).
* Login.
* From now on you can use `docker-compose up` instead of `docker-compose run app`.
* Check out `http://localhost:3333`.

I recommend to use NGINX to serve the UI over https. Here is a config example:

```
upstream sns_index_upstream {
  server 127.0.0.1:3333;
}

server {
  listen 80;
  return 301 https://$host$request_uri;
}

server {
  listen 443 ssl http2;
  ssl_certificate /etc/ssl/certs/sns-index.pem;
  ssl_certificate_key /etc/ssl/private/sns-index.key;

  server_name sns-index.com;
  location / {
    proxy_pass http://sns_index_upstream;
  }
}
```

## Caveats

* There is a bug of Telega library that suspends scraping for ~15 minutes sometimes.
* The scraper does not handle updated messages.
