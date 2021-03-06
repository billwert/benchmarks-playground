const express = require('express')
const request = require('request')
const app = express()
const port = 5000

var uri = process.env.FORECAST_SERVICE_URI || "http://localhost:8080";
if (uri.endsWith("/"))
{
    uri = uri.substr(0, uri.length - 1);
}

uri += "/forecast";

var options = {
    method: 'GET',
    uri: uri,
    resolveWithFullResponse: true,
    json: true
};

async function makeRequest (options) {
    return new Promise((resolve, reject) => {
        request(options, (error, response, body) => {
            if (error) {
                return reject(error);
            }
            return resolve({ body, response });
        })
    })
}
  

app.get('/', async (req, res) => 
{
    let r = await makeRequest(options);
    res.json({ location: 'Seattle', weather: r.body.weather });
});

app.listen(port, () => console.log(`app listening on port ${port}!`))