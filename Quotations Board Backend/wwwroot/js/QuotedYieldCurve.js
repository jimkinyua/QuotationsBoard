var myChart = echarts.init(document.getElementById("yeild-curve-area"));

// Make API call to get the data
// widget-script.js
(function () {
  var xhr = new XMLHttpRequest();
  var loaderElement = document.getElementById("loader"); // Get the loader element
  xhr.open(
    "POST",
    "https://quotations.nse.co.ke/api/Widgets/QuotedYieldCurve",
    true
  );
  xhr.setRequestHeader("Content-Type", "application/json");

  // Assuming your API expects a "Date" field in "yyyy-MM-dd" format.
  var dataToSend = JSON.stringify({
    Date: new Date().toISOString(),
  });

  loaderElement.style.display = "flex";

  xhr.onload = function () {
    if (xhr.status >= 200 && xhr.status < 300) {
      // Parse the response and use it to initialize the chart
      var responseData = JSON.parse(xhr.responseText);
      console.log(responseData);
      // var myChart = echarts.init(document.getElementById("yeild-curve-area"));
      // // Assuming 'myChart.setOption' or similar function to initialize the chart
      // myChart.setOption({
      //   // ... options based on responseData
      // });
    } else {
      // Handle errors, maybe display a message to the user
      console.error("Request failed with status:", xhr.status);
    }
  };

  xhr.onerror = function () {
    // Handle network errors
    console.error("Network Error");
  };

  xhr.send(dataToSend);
})();
