var myChart = echarts.init(document.getElementById("quoted-yeild-curve-area"));

// Make API call to get the data
// widget-script.js
(function () {
  var xhr = new XMLHttpRequest();
  var loaderElement = document.getElementById("quoted-loader"); // Get the loader element
  var myChart = echarts.init(
    document.getElementById("quoted-yeild-curve-area")
  );

  xhr.open(
    "POST",
    "https://quotations.agilebiz.co.ke/api/Widgets/QuotedYieldCurve",
    true
  );
  xhr.setRequestHeader("Content-Type", "application/json");
  let DateToUse = new Date().toISOString();
  let DateOnly = DateToUse.slice(0, 10);
  // Assuming your API expects a "Date" field in "yyyy-MM-dd" format.
  var dataToSend = JSON.stringify({
    Date: DateToUse,
  });

  loaderElement.style.display = "flex";

  xhr.onload = function () {
    if (xhr.status >= 200 && xhr.status < 300) {
      // Parse the response and use it to initialize the chart
      var responseData = JSON.parse(xhr.responseText);
      console.log(responseData);
      let YAxisData = [];
      let XAxisData = [];
      for (let i = 0; i < responseData.length; i++) {
        YAxisData.push(responseData[i].Tenure);
        XAxisData.push(responseData[i].Yield);
      }

      var option = {
        color: [
          "#8DC341",
          "#8DC341",
          "#8DC341",
          "#8DC341",
          "#8DC341",
          "#8DC341",
        ],
        title: {
          text: "Quoted Yield Curve as at " + DateOnly,
          textStyle: {
            fontWeight: "bold",
          },
          align: "center",
        },
        tooltip: {
          trigger: "axis",
        },
        xAxis: {
          type: "category",
          data: YAxisData,
          name: "Tenor (Years)",
          nameLocation: "end",
          nameTextStyle: {
            fontWeight: "bold",
          },
          alignTicks: true,
          splitLine: { show: true },
        },
        yAxis: {
          type: "value",
          name: "Yield",
          nameTextStyle: {
            fontWeight: "bold",
          },
          axisLine: {
            show: true,
          },
          min(value) {
            const minInt = Math.floor(value.min);
            return minInt - 1;
          },
        },
        series: [
          {
            data: XAxisData,
            type: "line",
            smooth: true,
            connectNulls: true,
          },
        ],
      };

      myChart.setOption(option);
      loaderElement.style.display = "none";
    } else {
      // get the error message from the response and log it

      var error = JSON.parse(xhr.responseText);
      alert(error.Message);
      console.error(error.Message);
      loaderElement.style.display = "none";
    }
  };

  xhr.onerror = function () {
    // Handle network errors
    console.error("Network Error");
  };

  xhr.send(dataToSend);
})();
