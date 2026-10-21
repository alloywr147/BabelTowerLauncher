import React from "react";
import { createRoot } from "react-dom/client";
import { StandardApp } from "./StandardApp.jsx";
import "./styles.css";
createRoot(document.getElementById("root")).render(<React.StrictMode><StandardApp/></React.StrictMode>);
