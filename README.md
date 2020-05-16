# Telegram Index

## Summary

Scrapes Telegram channel for #whois messages and provides UI to see them.
Check out [sns-index](http://sns-index.com).

## Hosting

The easiest way is to use the docker-compose:
* Clone the repository.
* Create `data` directory.
* Create `data/config.json`.
* Here is an example of `config.json`. All you need to change is `Scrapper.ChannelUsername`.
```JSON
{
  "Telegram": {
    "ApiId": 17349,
    "ApiHash": "344583e45741c457fe1862106095a5eb"
  },
  "Scrapper": {
    "ChannelUsername": "startupneversleeps"
  },
  "Trace": false,
  "DisableSync": false
}
```
* Run `docker-compose run app`.
* Login
* From now on you can use `docker-compose up` instead of `docker-compose run app`.
* Check out `http://localhost:3333`.
