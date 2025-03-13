## Statikk-Scraper

A highly efficient and compact data retrieval tool for the Riot Games API.

### Introduction

Currently there are a few 3rd Party Sites used by the majority of the playerbase for statistical analysis. These sites obtain their data through players using their site. As you update your profile, this updates the data in their system. Which is then what drives the recommendations these sites provide to you.

This makes it extremely difficult for new 3rd Part Sites to compete, as without an active large population of users continously updating their profiles. You will not have access to large amounts of real time data.

It's due to this, that this project has been created.

### Usage

Currently this script drives the data for the following site [Mosgi](https://www.mosgi.org).

### Details

One of the most important factors in statistical analysis of games, is the rank at which a match was played. Matches themselves don't have a rank, therefore it is common practice to calculate the average rank of all players of a match, and use that value.

This is effective, however the issue you have here is how up to date are the ranks for the players? The match data does not contain the information regarding the ranks of players. So you have two options:
- Maintain the rank for each summoner in each queue in your database
- Fetch the rank of each summoner within the match from the Riot Games Api

Option one is the simplest. However you then run into the issue of how up to date is the ranks for each of the summoners.

Option two on the other hand, provides the most accurate representation of the data. However this adds a lot of overhead to data insertation as well as you then run into the issue of when was the match played in respect the current rank of the summoners. If the match was played two days ago and you are setting the average rank to be the current ranks of the players. Is that still accurate?

These are the two considerations my script intends to solve. Firstly I want as much data as possible in regards to ranked solo duo games from NA, EUW, KR. Secondly I want to be able to accurately set a rank against each match for statistical analysis. With this in mind, we have the following process:
- Foreach region, and foreach division in each tier in a region
- We get the League Entries for that division, starting at page 1 and incrementing
- For each League Entry, we convert it into objects for my database.
- We then utilize the Puuid provided in the League Entries to fetch match ids from the day prior
